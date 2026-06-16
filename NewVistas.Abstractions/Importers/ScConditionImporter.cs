// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^DGS (VistA SERVICE CONNECTED CONDITIONS file #2.04) into embedded ScCondition entries
/// via the PatientWorkflowGrain.
/// </summary>
public class ScConditionImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public ScConditionImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var scGroups = records
            .Where(kvp => kvp.Key.Global == "DGS" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in scGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^Condition^ICD10^Percentage^IsServiceConnected^EffectiveDate(FM)^Extremity
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 1);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                string condition = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN CONDITION";
                string? icd10 = ZwrParser.Piece(zeroNode.Value, 3);

                int? percentage = null;
                string? percentStr = ZwrParser.Piece(zeroNode.Value, 4);
                if (!string.IsNullOrEmpty(percentStr) && int.TryParse(percentStr, out int parsedPct))
                    percentage = parsedPct;

                string? scStr = ZwrParser.Piece(zeroNode.Value, 5);
                bool isServiceConnected = scStr == "1";

                DateTime? effectiveDate = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 6));
                string? extremity = ZwrParser.Piece(zeroNode.Value, 7);

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.RecordServiceConnectedConditionAsync(
                    condition, icd10, percentage,
                    isServiceConnected, effectiveDate,
                    extremity,
                    null);              // comments

                result.RecordSuccess("ScCondition");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} SC conditions so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("ScCondition");
                _logger.LogError(ex, "Failed to import SC condition IEN {Ien}", group.Key);
            }
        }
    }
}
