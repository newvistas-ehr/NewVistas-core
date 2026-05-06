// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^AUPNPROB (VistA PROBLEM LIST file #9000011) into embedded ProblemEntry
/// on the patient grain. Problems are stored directly on the patient — no separate grain.
/// </summary>
public class ProblemImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public ProblemImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var problemGroups = records
            .Where(kvp => kvp.Key.Global == "AUPNPROB" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in problemGroups)
        {
            try
            {
                long ien = group.Key;
                string problemId = _ienMap.GetOrCreateKey("AUPNPROB", ien, "PROB");

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^AUPNPROB(ien,0) = Diagnosis(.01)^Condition^DateOnset^Status^...
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string diagnosis = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN";
                string? condition = ZwrParser.Piece(zeroNode.Value, 2);
                DateTime? dateOfOnset = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3));
                string? status = ZwrParser.Piece(zeroNode.Value, 4);

                // Patient DFN reference — piece 5 or from .02 field
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 5);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                // 1-node: ^AUPNPROB(ien,1) = DiagnosisCode^Priority^SC^Provider^...
                ZwrRecord? oneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "1");

                string? diagnosisCode = oneNode != null ? ZwrParser.Piece(oneNode.Value, 1) : null;
                string? priority = oneNode != null ? ZwrParser.Piece(oneNode.Value, 2) : null;
                string? scStr = oneNode != null ? ZwrParser.Piece(oneNode.Value, 3) : null;
                bool isServiceConnected = scStr == "1" || scStr?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true;

                // Determine resolved date if inactive
                DateTime? dateResolved = null;
                string entryStatus = "ACTIVE";
                if (status?.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase) == true)
                {
                    entryStatus = "INACTIVE";
                    string? resolvedStr = oneNode != null ? ZwrParser.Piece(oneNode.Value, 7) : null;
                    dateResolved = ZwrParser.ParseFmDate(resolvedStr) ?? DateTime.UtcNow;
                }

                var entry = new ProblemEntry
                {
                    ProblemId = problemId,
                    Diagnosis = diagnosis,
                    DiagnosisCode = diagnosisCode,
                    Status = entryStatus,
                    DateOfOnset = dateOfOnset,
                    DateResolved = dateResolved,
                    DateRecorded = DateTime.UtcNow,
                    IsServiceConnected = isServiceConnected,
                    Condition = condition,
                    Priority = priority,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };

                IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patientKey);
                await patient.AddProblemAsync(entry);

                result.RecordSuccess("Problem");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} problems so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Problem");
                _logger.LogError(ex, "Failed to import problem IEN {Ien}", group.Key);
            }
        }
    }
}
