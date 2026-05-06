// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^LR(63,...) (VistA LAB DATA file #63) into ILabTestGrain instances.
/// Focuses on Chemistry ("CH") sub-file results.
/// </summary>
public class LabImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public LabImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        // Lab data structure: ^LR(63,patientDFN,"CH",fmDate,seq) = ...^^^TestName^Value^...
        var labGroups = records
            .Where(kvp => kvp.Key.Global == "LR" && kvp.Key.FileNumber == "63")
            .GroupBy(kvp => kvp.Key.Ien);

        int labSeq = 0;

        foreach (var patientGroup in labGroups)
        {
            long patientDfn = patientGroup.Key;
            string patientKey = _ienMap.TryGetKey("DPT", patientDfn) ?? $"P{patientDfn}";

            List<ZwrRecord> allRecords = patientGroup.SelectMany(g => g.Value).ToList();

            // Filter to Chemistry results — subscripts[0] == "CH"
            var chemRecords = allRecords.Where(r =>
                r.Subscripts.Count >= 1 && r.Subscripts[0] == "CH").ToList();

            foreach (ZwrRecord labRecord in chemRecords)
            {
                try
                {
                    labSeq++;
                    string grainKey = $"LAB-IEN-{patientDfn}-CH-{labSeq}";

                    ILabTestGrain grain = _grainFactory.GetGrain<ILabTestGrain>(grainKey);

                    // ^LR(63,dfn,"CH",fmDate,seq) = ^^^TestName^Value^Units^RefLow^RefHigh^AbnFlag
                    string testName = ZwrParser.Piece(labRecord.Value, 4) ?? "UNKNOWN TEST";
                    string? resultValue = ZwrParser.Piece(labRecord.Value, 5);
                    string? resultUnit = ZwrParser.Piece(labRecord.Value, 6);
                    string? refLow = ZwrParser.Piece(labRecord.Value, 7);
                    string? refHigh = ZwrParser.Piece(labRecord.Value, 8);
                    string? abnFlag = ZwrParser.Piece(labRecord.Value, 9);

                    // Parse collection date from subscript (FM format)
                    DateTime? collectionDate = null;
                    if (labRecord.Subscripts.Count >= 2)
                        collectionDate = ZwrParser.ParseFmDate(labRecord.Subscripts[1]);

                    // Order the lab test
                    await grain.OrderLabTestAsync(
                        patientKey,
                        testName,
                        testName,
                        null, null,
                        null, null,
                        null,
                        "CHEMISTRY");

                    // Record collection
                    if (collectionDate.HasValue)
                        await grain.CollectSpecimenAsync(collectionDate.Value, null, null);

                    // Record result if present
                    if (!string.IsNullOrEmpty(resultValue))
                    {
                        DateTime resultDate = collectionDate ?? DateTime.UtcNow;
                        await grain.RecordResultAsync(
                            resultDate,
                            resultValue,
                            resultUnit,
                            refLow,
                            refHigh,
                            abnFlag);
                    }

                    // Link to patient grain
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        await patient.AddLabTestIdAsync(grainKey);
                    }

                    result.RecordSuccess("Lab");
                    if (labSeq % 50 == 0)
                        _logger.LogInformation("Imported {Count} lab results so far", labSeq);
                }
                catch (Exception ex)
                {
                    result.RecordError("Lab");
                    _logger.LogError(ex, "Failed to import lab result for patient IEN {Ien}", patientDfn);
                }
            }
        }
    }
}
