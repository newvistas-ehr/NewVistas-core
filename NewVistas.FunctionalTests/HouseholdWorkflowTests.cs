// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Phase 1 (Whole-Person Social Care) — the Person-anchored household: a patient resolves to their
/// household via the Person anchor, non-patient family members can join, membership is time-bounded
/// (leaving keeps history), and the surface is gated by the SOCIAL_CARE feature. NonParallelizable —
/// it toggles the global feature flag.
/// </summary>
[TestFixture, NonParallelizable]
public class HouseholdWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string Feature = "SOCIAL_CARE";
    private const string Clerk = "CLERK1";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(Feature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(Feature);

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IHouseholdGrain Household(string id) => _cluster.GrainFactory.GetGrain<IHouseholdGrain>(id);

    private async Task<string> NewPatientAsync(string name)
    {
        string pid = $"HH-{Guid.NewGuid()}";
        await Wf(pid).UpdateDemographicsAsync(name, "F", new DateTime(1980, 3, 1), null);
        return pid;
    }

    [Test]
    public async Task CreateHousehold_PatientResolvesToItAsHead()
    {
        string patient = await NewPatientAsync("SMITH,JANE");
        string householdId = await Wf(patient).CreateHouseholdForPatientAsync("Smith Household", "Self", "500", Clerk);

        HouseholdState? hh = await Wf(patient).GetPatientHouseholdAsync();
        Assert.That(hh, Is.Not.Null);
        Assert.That(hh!.HouseholdId, Is.EqualTo(householdId));
        Assert.That(hh.Members, Has.Count.EqualTo(1));
        Assert.That(hh.HeadOfHouseholdPersonId, Is.EqualTo(hh.Members[0].PersonId));
        Assert.That(hh.Members[0].Role, Is.EqualTo(HouseholdMemberRole.HeadOfHousehold));
    }

    [Test]
    public async Task AddNonPatientMember_AppearsInHousehold()
    {
        string patient = await NewPatientAsync("SMITH,JANE");
        string householdId = await Wf(patient).CreateHouseholdForPatientAsync("Smith Household", "Self", "500", Clerk);

        string childPersonId = await Wf(patient).AddNonPatientMemberToHouseholdAsync(
            householdId, "SMITH,TIMMY", new DateTime(2018, 6, 1), "M", "", "Son", HouseholdMemberRole.Child, Clerk);

        HouseholdState hh = await Household(householdId).GetAsync();
        Assert.That(hh.Members, Has.Count.EqualTo(2));
        Assert.That(hh.Members.Any(m => m.PersonId == childPersonId && m.Relationship == "Son"), Is.True);

        // The child's Person exists (bare identity, no patient role).
        PersonState child = await _cluster.GrainFactory.GetGrain<IPersonGrain>(childPersonId).GetAsync();
        Assert.That(child.Name, Is.EqualTo("SMITH,TIMMY"));
        Assert.That(child.PatientRoles, Is.Empty);
    }

    [Test]
    public async Task MemberLeaves_HistoryKept_IndexClosed()
    {
        string patient = await NewPatientAsync("SMITH,JANE");
        string householdId = await Wf(patient).CreateHouseholdForPatientAsync("Smith Household", "Self", "500", Clerk);
        string childPersonId = await Wf(patient).AddNonPatientMemberToHouseholdAsync(
            householdId, "SMITH,TIMMY", new DateTime(2000, 6, 1), "M", "", "Son", HouseholdMemberRole.Child, Clerk);

        // Currently a member of the household.
        IPersonHouseholdIndexGrain idx = _cluster.GrainFactory.GetGrain<IPersonHouseholdIndexGrain>($"PERSON-HOUSEHOLD-IDX:{childPersonId}");
        Assert.That(await idx.GetCurrentHouseholdIdAsync(), Is.EqualTo(householdId));

        await Household(householdId).RemoveMemberAsync(childPersonId, Clerk);

        HouseholdState hh = await Household(householdId).GetAsync();
        HouseholdMember child = hh.Members.Single(m => m.PersonId == childPersonId);
        Assert.That(child.LeftDate, Is.Not.Null, "membership is retained with a LeftDate (history)");
        Assert.That(await idx.GetCurrentHouseholdIdAsync(), Is.Null, "the person's open link is closed");
    }

    [Test]
    public async Task FlagOff_HouseholdIsEmpty()
    {
        string patient = await NewPatientAsync("SMITH,JANE");
        await Wf(patient).CreateHouseholdForPatientAsync("Smith Household", "Self", "500", Clerk);

        await SiteParams().DisableFeatureAsync(Feature);
        try
        {
            Assert.That(await Wf(patient).GetPatientHouseholdAsync(), Is.Null);
        }
        finally
        {
            await SiteParams().EnableFeatureAsync(Feature);
        }
    }

    [Test]
    public async Task PatientWithNoHousehold_ResolvesToNull()
    {
        string patient = await NewPatientAsync("LONE,WOLF");
        Assert.That(await Wf(patient).GetPatientHouseholdAsync(), Is.Null);
    }
}
