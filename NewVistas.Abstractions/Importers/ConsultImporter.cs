// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^GMR(123,...) (VistA REQUEST/CONSULTATION file #123) into IConsultGrain instances.
/// </summary>
public class ConsultImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public ConsultImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var consultGroups = records
            .Where(kvp => kvp.Key.Global == "GMR" && kvp.Key.FileNumber == "123")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in consultGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("GMR123", ien, "CONSULT");
                IConsultGrain grain = _grainFactory.GetGrain<IConsultGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^GMR(123,ien,0) = ToService(.01)^PatientDFN^Urgency^...
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string toService = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN SERVICE";

                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 2);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string patientKey = patientDfn > 0
                    ? (_ienMap.TryGetKey("DPT", patientDfn) ?? $"P{patientDfn}")
                    : "UNKNOWN";

                string urgency = ZwrParser.Piece(zeroNode.Value, 3) ?? "ROUTINE";
                string? fromService = ZwrParser.Piece(zeroNode.Value, 4);
                string? requestingProvider = ZwrParser.Piece(zeroNode.Value, 5);

                // Reason for request — sub-nodes ^GMR(123,ien,20,n,0)
                var reasonNodes = allRecords
                    .Where(r => r.Subscripts.Count >= 1 && r.Subscripts[0] == "20")
                    .OrderBy(r => r.Subscripts.Count > 1 ? r.Subscripts[1] : "0")
                    .ToList();

                string? reasonForRequest = reasonNodes.Count > 0
                    ? string.Join(" ", reasonNodes.Select(r => r.Value))
                    : null;

                await grain.RequestConsultAsync(
                    patientKey,
                    toService,
                    null,
                    fromService,
                    null,
                    urgency,
                    requestingProvider,
                    null,
                    null, null,
                    reasonForRequest,
                    null,
                    null,
                    null, null);

                // Link to patient grain
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        await patient.AddConsultIdAsync(grainKey);
                    }
                }

                result.RecordSuccess("Consult");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} consults so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Consult");
                _logger.LogError(ex, "Failed to import consult IEN {Ien}", group.Key);
            }
        }
    }
}
