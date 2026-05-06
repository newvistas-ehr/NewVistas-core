// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^GMR(120.8,...) (VistA PATIENT ALLERGIES file #120.8) into embedded AllergyEntry
/// on the patient grain. Allergies are stored directly on the patient — no separate grain.
/// </summary>
public class AllergyImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public AllergyImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var allergyGroups = records
            .Where(kvp => kvp.Key.Global == "GMR" && kvp.Key.FileNumber == "120.8")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in allergyGroups)
        {
            try
            {
                long ien = group.Key;
                string allergyId = _ienMap.GetOrCreateKey("GMR120.8", ien, "ALLERGY");

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^GMR(120.8,ien,0) = Allergen^AllergenType^Reactant^...
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string allergen = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN";
                string allergenType = ZwrParser.Piece(zeroNode.Value, 2) ?? "Drug";
                string? reactant = ZwrParser.Piece(zeroNode.Value, 3);

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 4);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                // Reactions — stored in sub-nodes ^GMR(120.8,ien,10,n,0)
                var reactions = new List<string>();
                foreach (ZwrRecord rxnNode in allRecords.Where(r =>
                    r.Subscripts.Count >= 2 && r.Subscripts[0] == "10"))
                {
                    string? reaction = ZwrParser.Piece(rxnNode.Value, 1);
                    if (!string.IsNullOrEmpty(reaction))
                        reactions.Add(reaction);
                }

                // Severity — often in a specific sub-node
                string? severity = null;
                ZwrRecord? sevNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "14.5");
                if (sevNode != null)
                    severity = sevNode.Value;

                // Observed/Historical
                string? observedHistorical = ZwrParser.Piece(zeroNode.Value, 6);

                var entry = new AllergyEntry
                {
                    AllergyId = allergyId,
                    Allergen = allergen,
                    AllergenType = allergenType,
                    AllergenId = reactant,
                    ReactionType = "ALLERGY",
                    Reactions = reactions,
                    Severity = severity,
                    ObservedHistorical = observedHistorical,
                    OriginationDateTime = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };

                IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patientKey);
                await patient.AddAllergyAsync(entry);

                result.RecordSuccess("Allergy");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} allergies so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Allergy");
                _logger.LogError(ex, "Failed to import allergy IEN {Ien}", group.Key);
            }
        }
    }
}
