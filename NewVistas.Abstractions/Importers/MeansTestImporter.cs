// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^DGMT (VistA MEANS TEST file #408.31) into embedded MeansTest entries
/// via the PatientWorkflowGrain.
/// </summary>
public class MeansTestImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public MeansTestImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var meansTestGroups = records
            .Where(kvp => kvp.Key.Global == "DGMT" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in meansTestGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^TestType^Date(FM)^Income^NetWorth^Dependents^EligStatus^PriorityGroup^ClerkDFN;VA(200,
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

                string testType = ZwrParser.Piece(zeroNode.Value, 2) ?? "MEANS TEST";
                DateTime dateOfTest = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;

                decimal? income = null;
                string? incomeStr = ZwrParser.Piece(zeroNode.Value, 4);
                if (!string.IsNullOrEmpty(incomeStr) && decimal.TryParse(incomeStr, out decimal parsedIncome))
                    income = parsedIncome;

                decimal? netWorth = null;
                string? netWorthStr = ZwrParser.Piece(zeroNode.Value, 5);
                if (!string.IsNullOrEmpty(netWorthStr) && decimal.TryParse(netWorthStr, out decimal parsedNetWorth))
                    netWorth = parsedNetWorth;

                int? dependents = null;
                string? dependentsStr = ZwrParser.Piece(zeroNode.Value, 6);
                if (!string.IsNullOrEmpty(dependentsStr) && int.TryParse(dependentsStr, out int parsedDependents))
                    dependents = parsedDependents;

                string? eligStatus = ZwrParser.Piece(zeroNode.Value, 7);
                string? priorityGroup = ZwrParser.Piece(zeroNode.Value, 8);

                // Clerk reference
                string? clerkDfnStr = ZwrParser.Piece(zeroNode.Value, 9);
                string? clerkId = null;
                string? clerkName = null;
                if (clerkDfnStr != null)
                {
                    long.TryParse(clerkDfnStr.Split(';')[0], out long clerkDfn);
                    if (clerkDfn > 0)
                    {
                        clerkId = _ienMap.TryGetKey("VA200", clerkDfn) ?? $"STAFF-{clerkDfn}";
                        clerkName = await ResolveProviderNameAsync(clerkId, clerkDfn);
                    }
                }

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.RecordMeansTestAsync(
                    testType, dateOfTest,
                    income, netWorth, dependents,
                    eligStatus, priorityGroup,
                    clerkId, clerkName,
                    null);              // comments

                result.RecordSuccess("MeansTest");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} means tests so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("MeansTest");
                _logger.LogError(ex, "Failed to import means test IEN {Ien}", group.Key);
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
