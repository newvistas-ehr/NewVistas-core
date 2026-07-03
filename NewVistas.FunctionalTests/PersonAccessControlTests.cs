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
/// ADR-002 Phase 4 — employee-patient privacy guard. Verifies: employee-patients are auto-flagged
/// sensitive; the treatment relationship is NEVER gated; break-the-glass is a soft attest-and-proceed
/// at the boundary; the patient's open-sharing choice is honored; every access is audited; and the
/// cross-role Person detail never leaks to an unauthorized viewer.
/// </summary>
[TestFixture]
public class PersonAccessControlTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IPersonGrain Person(string personId) => _cluster.GrainFactory.GetGrain<IPersonGrain>(personId);
    private INewPersonGrain Staff(string userId) => _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");
    private IPatientAccessControlGrain Pac(string patientId) => _cluster.GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");

    private async Task<(IPatientWorkflowGrain Wf, string PatientId)> NewPatientAsync(string name = "ACC,TEST")
    {
        string pid = $"ACC-PT-{Guid.NewGuid()}";
        var wf = Workflow(pid);
        await wf.UpdateDemographicsAsync(name, "F", new DateTime(1980, 1, 1), "666123456");
        return (wf, pid);
    }

    private async Task<string> NewStaffAsync(string name = "ACC,STAFF")
    {
        string userId = $"STAFF-{Guid.NewGuid()}";
        await Staff(userId).UpdateProfileAsync(name, "Registered Nurse", "RN", "NURSING", "NURSE", "NURSE",
            "Medical-Surgical", "INST-500", "VA MEDICAL CENTER", "DIV-500", "MAIN DIVISION");
        return userId;
    }

    // A patient chart whose owner is also on staff → auto-flagged employee-patient (sensitive).
    private async Task<(IPatientWorkflowGrain Wf, string PatientId, string PersonId)> NewEmployeePatientAsync()
    {
        var (wf, pid) = await NewPatientAsync("NIGHTINGALE,NORA");
        string userId = await NewStaffAsync("NIGHTINGALE,NORA");
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await Person(personId).LinkStaffAsync(userId, PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        return (wf, pid, personId);
    }

    [Test]
    public async Task EmployeePatient_AutoFlagsChartSensitive()
    {
        var (_, pid, _) = await NewEmployeePatientAsync();
        PatientAccessControlState pac = await Pac(pid).GetAccessControlAsync();
        Assert.That(pac.IsSensitive, Is.True);
        Assert.That(pac.SensitivityCategories, Does.Contain("EMPLOYEE"));
    }

    [Test]
    public async Task EmployeePatient_UnlinkStaff_ClearsSensitivity()
    {
        var (wf, pid, personId) = await NewEmployeePatientAsync();
        PersonState person = await Person(personId).GetAsync();
        string userId = person.StaffRoles.Single().UserId;

        await Person(personId).UnlinkStaffAsync(userId);

        PatientAccessControlState pac = await Pac(pid).GetAccessControlAsync();
        Assert.That(pac.SensitivityCategories, Does.Not.Contain("EMPLOYEE"));
        Assert.That(pac.IsSensitive, Is.False);   // no other sensitivity reason
    }

    [Test]
    public async Task OpenRecord_Access_Allowed()
    {
        var (wf, _) = await NewPatientAsync();
        PatientAccessDecision d = await wf.AccessPatientAsync("ANY-USER", "Any User", breakTheGlassAttested: false, justification: null);
        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.Allowed));
        Assert.That(d.Granted, Is.True);
    }

    [Test]
    public async Task Sensitive_TeamViewer_AllowedByRelationship_NoBreakTheGlass()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();
        await Pac(pid).AddAuthorizedProviderAsync("DR-TEAM");   // treatment relationship

        PatientAccessDecision d = await wf.AccessPatientAsync("DR-TEAM", "Dr Team", breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.False);   // the team is NEVER gated
    }

    [Test]
    public async Task Sensitive_NonTeamViewer_NoAttest_RequiresBreakTheGlass()
    {
        var (wf, _, _) = await NewEmployeePatientAsync();
        PatientAccessDecision d = await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: false, justification: null);
        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.RequiresBreakTheGlass));
        Assert.That(d.Granted, Is.False);            // soft block — attest to proceed
    }

    [Test]
    public async Task Sensitive_NonTeamViewer_Attested_AllowedByBreakTheGlass()
    {
        var (wf, _, _) = await NewEmployeePatientAsync();
        PatientAccessDecision d = await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: true, justification: "Covering the unit tonight.");
        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByBreakTheGlass));
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.True);
    }

    [Test]
    public async Task OpenSharing_Access_AllowedByOpenConsent_EvenWhenSensitive()
    {
        var (wf, _, _) = await NewEmployeePatientAsync();   // sensitive employee-patient...
        await wf.SetPatientSharePreferenceAsync(PatientSharePreference.OpenForTeachingAndResearch);  // ...who chose openness

        PatientAccessDecision d = await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByOpenConsent));
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.False);   // the patient's choice wins; no BTG needed
    }

    [Test]
    public async Task GetPersonForViewer_NonTeam_HidesCrossRole_UntilBreakTheGlass()
    {
        var (wf, _, _) = await NewEmployeePatientAsync();

        PersonViewResult blocked = await wf.GetPatientPersonForViewerAsync("STRANGER", "A Stranger", breakTheGlassAttested: false, justification: null);
        Assert.That(blocked.Decision.Outcome, Is.EqualTo(PatientAccessOutcome.RequiresBreakTheGlass));
        Assert.That(blocked.Person, Is.Null);        // cross-role status does NOT leak

        PersonViewResult attested = await wf.GetPatientPersonForViewerAsync("STRANGER", "A Stranger", breakTheGlassAttested: true, justification: "Emergency.");
        Assert.That(attested.Decision.Granted, Is.True);
        Assert.That(attested.Person, Is.Not.Null);
        Assert.That(attested.Person!.StaffRoles, Is.Not.Empty);   // now the cross-role detail is visible
    }

    [Test]
    public async Task GetPersonForViewer_TeamViewer_ReturnsPerson_NoBreakTheGlass()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();
        await Pac(pid).AddAuthorizedProviderAsync("DR-TEAM");

        PersonViewResult r = await wf.GetPatientPersonForViewerAsync("DR-TEAM", "Dr Team", breakTheGlassAttested: false, justification: null);
        Assert.That(r.Decision.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(r.Person, Is.Not.Null);
    }

    [Test]
    public async Task EveryAccess_IsAudited_IncludingPendingBreakTheGlass()
    {
        var (wf, pid, _) = await NewEmployeePatientAsync();
        await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: false, justification: null);   // pending-BTG attempt
        await wf.AccessPatientAsync("STRANGER", "A Stranger", breakTheGlassAttested: true, justification: "Reason.");// BTG access

        List<PatientAccessLog> log = await Pac(pid).GetAccessLogAsync();
        Assert.That(log, Has.Some.Matches<PatientAccessLog>(e => e.AccessReason == "BLOCKED_PENDING_BTG"));
        Assert.That(log, Has.Some.Matches<PatientAccessLog>(e => e.WasBreakTheGlass && e.JustificationText == "Reason."));
    }
}
