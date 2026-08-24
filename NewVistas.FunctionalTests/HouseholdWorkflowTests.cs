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
    public async Task AddPatientToHousehold_MembershipVisibleFromBothSides()
    {
        string head = await NewPatientAsync("SMITH,JANE");
        string householdId = await Wf(head).CreateHouseholdForPatientAsync("Smith Household", "Self", "500", Clerk);

        string spouse = await NewPatientAsync("SMITH,ALEX");
        await Wf(spouse).AddPatientToHouseholdAsync(householdId, "Spouse", HouseholdMemberRole.Spouse, "500", Clerk);

        // Joining mints/links the patient's Person anchor.
        string? spousePersonId = (await Wf(spouse).GetPatientAsync()).PersonId;
        Assert.That(spousePersonId, Is.Not.Null.And.Not.Empty);

        // Household side: an active member row under that Person.
        HouseholdState hh = await Household(householdId).GetAsync();
        HouseholdMember member = hh.Members.Single(m => m.PersonId == spousePersonId);
        Assert.That(member.Relationship, Is.EqualTo("Spouse"));
        Assert.That(member.Role, Is.EqualTo(HouseholdMemberRole.Spouse));
        Assert.That(member.LeftDate, Is.Null);
        Assert.That(hh.HeadOfHouseholdPersonId, Is.Not.EqualTo(spousePersonId),
            "joining as a non-head must not displace the head");

        // Patient side: the spouse resolves to the same household.
        HouseholdState? resolved = await Wf(spouse).GetPatientHouseholdAsync();
        Assert.That(resolved?.HouseholdId, Is.EqualTo(householdId));
    }

    [Test]
    public async Task AddPatientToHousehold_Twice_UpdatesInPlaceWithoutDuplicating()
    {
        string head = await NewPatientAsync("SMITH,JANE");
        string householdId = await Wf(head).CreateHouseholdForPatientAsync("Smith Household", "Self", "500", Clerk);

        string other = await NewPatientAsync("SMITH,SAM");
        await Wf(other).AddPatientToHouseholdAsync(householdId, "Sibling", HouseholdMemberRole.Member, "500", Clerk);
        await Wf(other).AddPatientToHouseholdAsync(householdId, "Spouse", HouseholdMemberRole.Spouse, "500", Clerk);

        string? personId = (await Wf(other).GetPatientAsync()).PersonId;
        HouseholdState hh = await Household(householdId).GetAsync();
        List<HouseholdMember> rows = hh.Members.Where(m => m.PersonId == personId).ToList();
        Assert.That(rows, Has.Count.EqualTo(1), "re-adding an active member must not duplicate the row");
        Assert.That(rows[0].Relationship, Is.EqualTo("Spouse"), "descriptive fields update in place");
        Assert.That(rows[0].Role, Is.EqualTo(HouseholdMemberRole.Spouse));
        Assert.That(rows[0].LeftDate, Is.Null);
    }

    [Test]
    public async Task AddPatientToSecondHousehold_BothMembershipsOpen_CurrentIsLatestJoined()
    {
        string headA = await NewPatientAsync("ALPHA,HEAD");
        string headB = await NewPatientAsync("BRAVO,HEAD");
        string hhA = await Wf(headA).CreateHouseholdForPatientAsync("Alpha Household", "Self", "500", Clerk);
        string hhB = await Wf(headB).CreateHouseholdForPatientAsync("Bravo Household", "Self", "500", Clerk);

        string patient = await NewPatientAsync("SHARED,KID");
        await Wf(patient).AddPatientToHouseholdAsync(hhA, "Child", HouseholdMemberRole.Child, "500", Clerk);
        await Wf(patient).AddPatientToHouseholdAsync(hhB, "Child", HouseholdMemberRole.Child, "500", Clerk);

        string? personId = (await Wf(patient).GetPatientAsync()).PersonId;

        // Membership in a second household is allowed (shared custody is representable):
        // both households carry an open row for the same Person …
        Assert.That((await Household(hhA).GetAsync()).Members
            .Any(m => m.PersonId == personId && m.LeftDate is null), Is.True);
        Assert.That((await Household(hhB).GetAsync()).Members
            .Any(m => m.PersonId == personId && m.LeftDate is null), Is.True);

        // … the person's reverse index holds both open links …
        IPersonHouseholdIndexGrain idx = _cluster.GrainFactory
            .GetGrain<IPersonHouseholdIndexGrain>($"PERSON-HOUSEHOLD-IDX:{personId}");
        PersonHouseholdIndexState links = await idx.GetAsync();
        Assert.That(links.Links.Count(l => l.LeftDate is null), Is.EqualTo(2));

        // … and "the patient's household" resolves to the most recently joined.
        HouseholdState? current = await Wf(patient).GetPatientHouseholdAsync();
        Assert.That(current?.HouseholdId, Is.EqualTo(hhB));
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
