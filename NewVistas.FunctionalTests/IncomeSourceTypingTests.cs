// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// R1 (Whole-Person Social Care roadmap) — itemized income-source typing on the means-test income
/// household: a member's annual income is the sum of typed sources (wages / SSDI / …) when present,
/// and falls back to the single gross figure otherwise. Household totals reflect it.
/// </summary>
[TestFixture]
public class IncomeSourceTypingTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IIncomeHouseholdGrain Household(string patientId) =>
        _cluster.GrainFactory.GetGrain<IIncomeHouseholdGrain>($"INCOME-HOUSEHOLD:{patientId}");

    private static IncomeSourceItem S(IncomeSourceType t, decimal amt) => new() { SourceType = t, Amount = amt };

    [Test]
    public async Task ItemizedSources_SumToMemberIncome_AndHouseholdTotal()
    {
        string patient = $"INC-{Guid.NewGuid()}";
        IIncomeHouseholdGrain hh = Household(patient);

        string personId = await hh.AddOrUpdateMemberAsync(new IncomePerson
        {
            Name = "VET,SELF", RelationshipType = "SELF", IsVeteranSelf = true,
            IncomeSources = new()
            {
                S(IncomeSourceType.Wages, 30000m),
                S(IncomeSourceType.SocialSecurityDisability, 12000m)
            }
        });

        IncomeHouseholdState state = await hh.GetAsync();
        Assert.That(state.TotalHouseholdIncome, Is.EqualTo(42000m));
        Assert.That(state.HouseholdMembers.Single(m => m.PersonId == personId).IncomeSources, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SetIncomeSources_ReplacesBreakdown_AndRecomputesTotal()
    {
        string patient = $"INC-{Guid.NewGuid()}";
        IIncomeHouseholdGrain hh = Household(patient);
        string personId = await hh.AddOrUpdateMemberAsync(new IncomePerson { Name = "A", GrossAnnualIncome = 5000m });

        // Gross-only member counts via the gross figure.
        Assert.That((await hh.GetAsync()).TotalHouseholdIncome, Is.EqualTo(5000m));

        // Adding an itemized breakdown becomes authoritative.
        await hh.SetMemberIncomeSourcesAsync(personId, new()
        {
            S(IncomeSourceType.SupplementalSecurityIncome, 9000m),
            S(IncomeSourceType.ChildSupportOrAlimony, 3000m)
        });
        Assert.That((await hh.GetAsync()).TotalHouseholdIncome, Is.EqualTo(12000m));
    }

    [Test]
    public async Task MixedMembers_GrossAndItemized_BothCount()
    {
        string patient = $"INC-{Guid.NewGuid()}";
        IIncomeHouseholdGrain hh = Household(patient);
        await hh.AddOrUpdateMemberAsync(new IncomePerson { Name = "GROSS", GrossAnnualIncome = 20000m });
        await hh.AddOrUpdateMemberAsync(new IncomePerson { Name = "ITEMIZED", IncomeSources = new() { S(IncomeSourceType.PrivatePension, 10000m) } });

        Assert.That((await hh.GetAsync()).TotalHouseholdIncome, Is.EqualTo(30000m));
    }
}
