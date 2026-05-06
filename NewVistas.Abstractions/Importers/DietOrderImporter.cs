// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^FH (VistA DIETETICS file #115.2) into DietOrder entries
/// via the PatientWorkflowGrain.
/// </summary>
public class DietOrderImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public DietOrderImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var dietGroups = records
            .Where(kvp => kvp.Key.Global == "FH" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in dietGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^DietType^CurrentDiet^Modifications^Texture^FluidConsist^CalLevel^StartDate(FM)^ProviderDFN;VA(200,
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

                string dietType = ZwrParser.Piece(zeroNode.Value, 2) ?? "REGULAR";
                string? currentDiet = ZwrParser.Piece(zeroNode.Value, 3);

                // Modifications may be semicolon-separated
                List<string>? modifications = null;
                string? modStr = ZwrParser.Piece(zeroNode.Value, 4);
                if (!string.IsNullOrEmpty(modStr))
                    modifications = modStr.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                string? texture = ZwrParser.Piece(zeroNode.Value, 5);
                string? fluidConsist = ZwrParser.Piece(zeroNode.Value, 6);
                string? calLevel = ZwrParser.Piece(zeroNode.Value, 7);
                DateTime startDateTime = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 8))
                    ?? DateTime.UtcNow;

                // Provider reference
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 9);
                string? providerId = null;
                string? providerName = null;
                if (providerDfnStr != null)
                {
                    long.TryParse(providerDfnStr.Split(';')[0], out long providerDfn);
                    if (providerDfn > 0)
                    {
                        providerId = _ienMap.TryGetKey("VA200", providerDfn) ?? $"STAFF-{providerDfn}";
                        providerName = await ResolveProviderNameAsync(providerId, providerDfn);
                    }
                }

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.CreateDietOrderAsync(
                    dietType, currentDiet, modifications,
                    texture, fluidConsist, calLevel,
                    null,               // specialInstructions
                    startDateTime,
                    providerId, providerName,
                    null);              // comments

                result.RecordSuccess("DietOrder");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} diet orders so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("DietOrder");
                _logger.LogError(ex, "Failed to import diet order IEN {Ien}", group.Key);
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
