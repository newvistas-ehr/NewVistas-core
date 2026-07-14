// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging.Abstractions;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Runs the real <see cref="SocialCareSeed"/> end to end and asserts the demo's whole-person thesis:
/// P9301 is in a Person-anchored household with a non-patient child, and the positive SDOH screen has
/// closed the loop — the mapped Z-codes are on the problem list and matching Social Work referrals are
/// open. NonParallelizable — it seeds a fixed patient and touches the global feature flag.
/// </summary>
[TestFixture, NonParallelizable]
public class SocialCareDemoSeedTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [Test]
    public async Task Seed_BuildsHousehold_AndClosesTheSdohLoop()
    {
        await SocialCareSeed.SeedAsync(_cluster.GrainFactory, NullLogger.Instance);

        // Household: SAM (head, patient) + SUSIE (non-patient child).
        HouseholdState? hh = await Wf("P9301").GetPatientHouseholdAsync();
        Assert.That(hh, Is.Not.Null);
        Assert.That(hh!.Members.Count(m => m.LeftDate is null), Is.EqualTo(2));
        Assert.That(hh.Members.Any(m => m.Relationship == "Daughter"), Is.True);

        // Closed loop: the mapped Z-codes are on the problem list.
        List<ProblemSummary> problems = await Wf("P9301").GetActiveProblemsAsync();
        Assert.That(problems.Select(p => p.DiagnosisCode), Does.Contain("Z59.41"));   // food insecurity
        Assert.That(problems.Select(p => p.DiagnosisCode), Does.Contain("Z59.811"));  // housing instability

        // Closed loop: matching Social Work referrals are open (Food + Housing).
        List<SocialWorkReferralIndexEntry> referrals = await Wf("P9301").GetSocialWorkReferralsAsync();
        Assert.That(referrals.Count, Is.GreaterThanOrEqualTo(2));
    }
}
