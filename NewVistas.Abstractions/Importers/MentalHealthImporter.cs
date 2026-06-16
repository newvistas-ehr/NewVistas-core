// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^YTT(601,...) (VistA MENTAL HEALTH INSTRUMENT RESULTS file #601) into
/// MentalHealthScreen grains via the PatientWorkflowGrain.
/// </summary>
public class MentalHealthImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public MentalHealthImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var mentalHealthGroups = records
            .Where(kvp => kvp.Key.Global == "YTT" && kvp.Key.FileNumber == "601")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in mentalHealthGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: InstrumentName^PatientDFN;DPT(^Date(FM)^TotalScore^Interpretation^IsPositive^ProviderDFN;VA(200,
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string instrumentName = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN";

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 2);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                DateTime administrationDateTime = ZwrParser.ParseFmDate(
                    ZwrParser.Piece(zeroNode.Value, 3)) ?? DateTime.UtcNow;

                decimal? totalScore = null;
                string? totalScoreStr = ZwrParser.Piece(zeroNode.Value, 4);
                if (!string.IsNullOrEmpty(totalScoreStr) && decimal.TryParse(totalScoreStr, out decimal score))
                    totalScore = score;

                string? interpretation = ZwrParser.Piece(zeroNode.Value, 5);

                bool? isPositive = null;
                string? isPositiveStr = ZwrParser.Piece(zeroNode.Value, 6);
                if (isPositiveStr == "1") isPositive = true;
                else if (isPositiveStr == "0") isPositive = false;

                // Provider reference
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 7);
                string? adminById = null;
                string? adminByName = null;
                if (providerDfnStr != null)
                {
                    long.TryParse(providerDfnStr.Split(';')[0], out long providerDfn);
                    if (providerDfn > 0)
                    {
                        adminById = _ienMap.TryGetKey("VA200", providerDfn) ?? $"STAFF-{providerDfn}";
                        adminByName = await ResolveProviderNameAsync(adminById, providerDfn);
                    }
                }

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.RecordMentalHealthScreenAsync(
                    instrumentName, administrationDateTime,
                    totalScore, interpretation, isPositive,
                    null,
                    adminById, adminByName,
                    null, null, null);

                result.RecordSuccess("MentalHealth");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} mental health screens so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("MentalHealth");
                _logger.LogError(ex, "Failed to import mental health screen IEN {Ien}", group.Key);
            }
        }
    }

    private async Task<string> ResolveProviderNameAsync(string providerKey, long providerDfn)
    {
        try
        {
            INewPersonGrain person = _grainFactory.GetGrain<INewPersonGrain>(providerKey);
            string name = await person.GetDisplayNameAsync();
            return string.IsNullOrEmpty(name) ? $"Provider {providerDfn}" : name;
        }
        catch
        {
            return $"Provider {providerDfn}";
        }
    }
}
