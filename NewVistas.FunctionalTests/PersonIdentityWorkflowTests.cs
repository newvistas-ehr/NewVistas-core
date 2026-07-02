// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Person identity (ADR-002) — the cross-role anchor. Verifies patient/staff/
/// relative linking, the employee-patient flag, the non-drifting back-pointers, and the two demo
/// cases (nurse-who-is-a-patient, mother-who-is-a-relative). End-to-end on the shared TestCluster.
/// </summary>
[TestFixture]
public class PersonIdentityWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IPersonGrain Person(string personId) => _cluster.GrainFactory.GetGrain<IPersonGrain>(personId);
    private INewPersonGrain Staff(string userId) => _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");
    private IPersonIndexGrain PersonIndex() => _cluster.GrainFactory.GetGrain<IPersonIndexGrain>("PERSON-INDEX:DEFAULT");

    private async Task<(IPatientWorkflowGrain Wf, string PatientId)> NewPatientAsync(string name = "PERSON,TEST")
    {
        string pid = $"PSN-PT-{Guid.NewGuid()}";
        var wf = Workflow(pid);
        await wf.UpdateDemographicsAsync(name, "F", new DateTime(1980, 1, 1), "666123456");
        return (wf, pid);
    }

    private async Task<string> NewStaffAsync(string name = "STAFF,TEST", string service = "NURSING")
    {
        string userId = $"STAFF-{Guid.NewGuid()}";
        await Staff(userId).UpdateProfileAsync(name, "Registered Nurse", "RN", service, "NURSE", "NURSE",
            "Medical-Surgical", "INST-500", "VA MEDICAL CENTER", "DIV-500", "MAIN DIVISION");
        return userId;
    }

    [Test]
    public async Task CreateOrGetPerson_LinksPatient_SetsBackPointerAndRole()
    {
        var (wf, pid) = await NewPatientAsync();
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        Assert.That(personId, Is.Not.Empty);
        PatientState patient = await wf.GetPatientAsync();
        Assert.That(patient.PersonId, Is.EqualTo(personId));      // back-pointer set

        PersonState person = await Person(personId).GetAsync();
        Assert.That(person.PatientRoles.Select(r => r.PatientId), Does.Contain(pid));  // reverse role
    }

    [Test]
    public async Task CreateOrGetPerson_IsIdempotent()
    {
        var (wf, _) = await NewPatientAsync();
        string first = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        string second = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        Assert.That(second, Is.EqualTo(first));
        PersonState person = await Person(first).GetAsync();
        Assert.That(person.PatientRoles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LinkStaff_SetsBackPointer_AndFlagsEmployeePatient()
    {
        var (wf, _) = await NewPatientAsync("NIGHTINGALE,NORA");
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        string userId = await NewStaffAsync("NIGHTINGALE,NORA");

        await Person(personId).LinkStaffAsync(userId, PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        NewPersonState staff = await Staff(userId).GetPersonAsync();
        Assert.That(staff.PersonId, Is.EqualTo(personId));       // back-pointer set on the staff record

        PersonState person = await Person(personId).GetAsync();
        Assert.That(person.StaffRoles.Select(r => r.UserId), Does.Contain(userId));
        Assert.That(person.IsEmployeePatient, Is.True);          // has both a patient- and a staff-role
    }

    [Test]
    public async Task NurseWhoIsAPatient_OnePerson_HoldsBothRoles()
    {
        var (wf, pid) = await NewPatientAsync("HEALER,HELEN");
        string userId = await NewStaffAsync("HEALER,HELEN");
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await Person(personId).LinkStaffAsync(userId, PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        PersonState person = await Person(personId).GetAsync();
        Assert.That(person.PatientRoles.Select(r => r.PatientId), Does.Contain(pid));
        Assert.That(person.StaffRoles.Select(r => r.UserId), Does.Contain(userId));
        Assert.That(person.IsEmployeePatient, Is.True);
    }

    [Test]
    public async Task LinkFamilyMember_SetsLinkedPersonId_AndRelativeAppearance()
    {
        // KAY is a patient; she is the "Mother" family-history entry on KIM's chart.
        var (kay, kayId) = await NewPatientAsync("KINDRED,KAY");
        var (kim, kimId) = await NewPatientAsync("KINDRED,KIM");

        string motherEntryId = await kim.AddFamilyMemberAsync(
            FamilyRelationship.Mother, "KINDRED,KAY", "F", FamilyVitalStatus.Alive, 62, null, "", "Also a patient.");
        string personKay = await kay.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        await kim.LinkFamilyMemberToPersonAsync(motherEntryId, personKay, "TEST");

        FamilyHistoryState fh = await kim.GetFamilyHistoryAsync();
        FamilyMemberHistoryEntry mother = fh.Members.Single(m => m.MemberId == motherEntryId);
        Assert.That(mother.LinkedPersonId, Is.EqualTo(personKay));   // the relative entry is linked

        PersonState person = await Person(personKay).GetAsync();
        Assert.That(person.PatientRoles.Select(r => r.PatientId), Does.Contain(kayId));   // she is a patient
        Assert.That(person.RelativeAppearances, Has.Some.Matches<PersonRelativeAppearance>(
            a => a.OnPatientId == kimId && a.Relationship == "Mother"));                  // and a relative on KIM's chart
    }

    [Test]
    public async Task GetPatientPerson_ReturnsNull_WhenUnlinked()
    {
        var (wf, _) = await NewPatientAsync();
        PersonState? person = await wf.GetPatientPersonAsync();
        Assert.That(person, Is.Null);
    }

    [Test]
    public async Task GetPatientPerson_ReturnsPerson_WhenLinked()
    {
        var (wf, _) = await NewPatientAsync();
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        PersonState? person = await wf.GetPatientPersonAsync();
        Assert.That(person, Is.Not.Null);
        Assert.That(person!.PersonId, Is.EqualTo(personId));
    }

    [Test]
    public async Task UnlinkPatient_ClearsBackPointer_AndRole()
    {
        var (wf, pid) = await NewPatientAsync();
        string userId = await NewStaffAsync();
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await Person(personId).LinkStaffAsync(userId, PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        await Person(personId).UnlinkPatientAsync(pid);

        PatientState patient = await wf.GetPatientAsync();
        Assert.That(patient.PersonId, Is.Null);                  // back-pointer cleared
        PersonState person = await Person(personId).GetAsync();
        Assert.That(person.PatientRoles, Is.Empty);
        Assert.That(person.IsEmployeePatient, Is.False);         // no longer both roles
    }

    [Test]
    public async Task PersonIndex_SearchByName_FindsRegisteredPerson()
    {
        string unique = $"ZID{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var (wf, _) = await NewPatientAsync($"{unique},PAT");
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");

        var hits = await PersonIndex().SearchByNameAsync(unique);
        Assert.That(hits.Select(h => h.PersonId), Does.Contain(personId));
    }
}
