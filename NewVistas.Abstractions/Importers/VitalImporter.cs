// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^GMR(120.5,...) (VistA GMRV VITAL MEASUREMENT file #120.5) into IVitalGrain instances.
/// </summary>
public class VitalImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public VitalImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var vitalGroups = records
            .Where(kvp => kvp.Key.Global == "GMR" && kvp.Key.FileNumber == "120.5")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in vitalGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("GMR120.5", ien, "VITAL");
                IVitalGrain grain = _grainFactory.GetGrain<IVitalGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^GMR(120.5,ien,0) = DateTimeTaken(.01)^VitalType^Value^PatientDFN
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                DateTime dateTimeTaken = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 1))
                    ?? DateTime.UtcNow;
                string vitalType = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN";
                string value = ZwrParser.Piece(zeroNode.Value, 3) ?? "0";

                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 4);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string patientKey = patientDfn > 0
                    ? (_ienMap.TryGetKey("DPT", patientDfn) ?? $"P{patientDfn}")
                    : "UNKNOWN";

                // Qualifiers — stored in sub-nodes
                List<string>? qualifiers = null;
                var qualNodes = allRecords.Where(r =>
                    r.Subscripts.Count >= 2 && r.Subscripts[0] == "5").ToList();
                if (qualNodes.Count > 0)
                {
                    qualifiers = qualNodes
                        .Select(q => ZwrParser.Piece(q.Value, 1))
                        .Where(q => !string.IsNullOrEmpty(q))
                        .Cast<string>()
                        .ToList();
                }

                await grain.RecordVitalAsync(
                    patientKey,
                    vitalType,
                    value,
                    null,
                    dateTimeTaken,
                    null, null,
                    null, null,
                    qualifiers,
                    null);

                // Register in patient vital index and legacy VitalIds
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        await patient.AddVitalIdAsync(grainKey);

                        IPatientVitalIndexGrain vitalIndex =
                            _grainFactory.GetGrain<IPatientVitalIndexGrain>(patKey);
                        await vitalIndex.AddVitalKeyAsync(grainKey, dateTimeTaken, vitalType);
                    }
                }

                result.RecordSuccess("Vital");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} vitals so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Vital");
                _logger.LogError(ex, "Failed to import vital IEN {Ien}", group.Key);
            }
        }
    }
}
