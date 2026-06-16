// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^NURS(210,...) (VistA NURSING ASSESSMENT file #210) into NursingAssessment grains
/// via the PatientWorkflowGrain.
/// </summary>
public class NursingImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public NursingImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var nursingGroups = records
            .Where(kvp => kvp.Key.Global == "NURS" && kvp.Key.FileNumber == "210")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in nursingGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^AssessType^DateTime(FM)^NurseDFN;VA(200,
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

                string assessType = ZwrParser.Piece(zeroNode.Value, 2) ?? "HEAD-TO-TOE";
                DateTime assessDateTime = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;

                // Nurse reference
                string? nurseDfnStr = ZwrParser.Piece(zeroNode.Value, 4);
                string nurseId = "UNKNOWN";
                string nurseName = "Unknown Nurse";
                if (nurseDfnStr != null)
                {
                    long.TryParse(nurseDfnStr.Split(';')[0], out long nurseDfn);
                    if (nurseDfn > 0)
                    {
                        nurseId = _ienMap.TryGetKey("VA200", nurseDfn) ?? $"STAFF-{nurseDfn}";
                        nurseName = await ResolveProviderNameAsync(nurseId, nurseDfn);
                    }
                }

                // Node 1: LOC^Orientation^BreathSounds^O2Therapy^SpO2
                ZwrRecord? oneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "1");

                string? locationId = oneNode != null ? ZwrParser.Piece(oneNode.Value, 1) : null;
                List<string>? orientation = null;
                string? orientationStr = oneNode != null ? ZwrParser.Piece(oneNode.Value, 2) : null;
                if (!string.IsNullOrEmpty(orientationStr))
                    orientation = orientationStr.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                string? breathSounds = oneNode != null ? ZwrParser.Piece(oneNode.Value, 3) : null;
                string? oxygenTherapy = oneNode != null ? ZwrParser.Piece(oneNode.Value, 4) : null;
                decimal? spO2 = ParseDecimal(oneNode != null ? ZwrParser.Piece(oneNode.Value, 5) : null);

                // Node 2: HeartRhythm^Edema^Skin^BradenScore^MorseScore
                ZwrRecord? twoNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "2");

                string? heartRhythm = twoNode != null ? ZwrParser.Piece(twoNode.Value, 1) : null;
                string? edema = twoNode != null ? ZwrParser.Piece(twoNode.Value, 2) : null;
                string? skin = twoNode != null ? ZwrParser.Piece(twoNode.Value, 3) : null;
                int? bradenScore = ParseInt(twoNode != null ? ZwrParser.Piece(twoNode.Value, 4) : null);
                int? morseScore = ParseInt(twoNode != null ? ZwrParser.Piece(twoNode.Value, 5) : null);

                // Node 3: Pain^PainLocation^BowelSounds^Appetite^UrineOutput^Foley
                ZwrRecord? threeNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "3");

                int? painScore = ParseInt(threeNode != null ? ZwrParser.Piece(threeNode.Value, 1) : null);
                string? painLocation = threeNode != null ? ZwrParser.Piece(threeNode.Value, 2) : null;
                string? bowelSounds = threeNode != null ? ZwrParser.Piece(threeNode.Value, 3) : null;
                string? appetite = threeNode != null ? ZwrParser.Piece(threeNode.Value, 4) : null;
                decimal? urineOutput = ParseDecimal(threeNode != null ? ZwrParser.Piece(threeNode.Value, 5) : null);
                bool hasFoley = (threeNode != null ? ZwrParser.Piece(threeNode.Value, 6) : null) == "Y";

                // Node 4: Anxiety^Mood^Mobility^FallRisk^Notes
                ZwrRecord? fourNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "4");

                string? anxiety = fourNode != null ? ZwrParser.Piece(fourNode.Value, 1) : null;
                string? mood = fourNode != null ? ZwrParser.Piece(fourNode.Value, 2) : null;
                string? mobility = fourNode != null ? ZwrParser.Piece(fourNode.Value, 3) : null;
                string? fallRisk = fourNode != null ? ZwrParser.Piece(fourNode.Value, 4) : null;
                string? notes = fourNode != null ? ZwrParser.Piece(fourNode.Value, 5) : null;

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.CreateNursingAssessmentAsync(
                    assessDateTime,
                    assessType,
                    nurseId,
                    nurseName,
                    locationId,
                    null,
                    null,               // levelOfConsciousness
                    orientation,
                    breathSounds,
                    oxygenTherapy,
                    spO2,
                    heartRhythm,
                    edema,
                    skin,
                    bradenScore,
                    painScore,
                    painLocation,
                    bowelSounds,
                    appetite,
                    urineOutput,
                    hasFoley,
                    anxiety,
                    mood,
                    morseScore,
                    fallRisk,
                    null,               // fallPrecautions
                    mobility,
                    notes);

                result.RecordSuccess("Nursing");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} nursing assessments so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Nursing");
                _logger.LogError(ex, "Failed to import nursing assessment IEN {Ien}", group.Key);
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

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return decimal.TryParse(value, out decimal result) ? result : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return int.TryParse(value, out int result) ? result : null;
    }
}
