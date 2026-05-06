// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^AUPNHF (VistA HEALTH FACTORS file #9000010.23) into HealthFactor grains
/// via the PatientWorkflowGrain.
/// </summary>
public class HealthFactorImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public HealthFactorImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var healthFactorGroups = records
            .Where(kvp => kvp.Key.Global == "AUPNHF" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in healthFactorGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: FactorName^Category^Date(FM)^Level^PatientDFN;DPT(^ProviderDFN;VA(200,
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string factorName = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN";
                string? category = ZwrParser.Piece(zeroNode.Value, 2);
                DateTime eventDateTime = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;
                string? level = ZwrParser.Piece(zeroNode.Value, 4);

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 5);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                // Provider reference
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 6);
                string? enteredById = null;
                string? enteredByName = null;
                if (providerDfnStr != null)
                {
                    long.TryParse(providerDfnStr.Split(';')[0], out long providerDfn);
                    if (providerDfn > 0)
                    {
                        enteredById = _ienMap.TryGetKey("VA200", providerDfn) ?? $"STAFF-{providerDfn}";
                        enteredByName = await ResolveProviderNameAsync(enteredById, providerDfn);
                    }
                }

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.RecordHealthFactorAsync(
                    factorName, category, eventDateTime,
                    level,
                    null,               // visitId
                    null, null,         // locationId, locationName
                    enteredById, enteredByName,
                    null);              // comments

                result.RecordSuccess("HealthFactor");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} health factors so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("HealthFactor");
                _logger.LogError(ex, "Failed to import health factor IEN {Ien}", group.Key);
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
