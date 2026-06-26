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
/// Functional tests for Care Team workflow — PCMM File #404.43.
/// Tests end-to-end: appointment scheduling → auto care team → access control → messaging.
/// </summary>
[TestFixture]
public class CareTeamWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private ICareTeamGrain GetCareTeam(string patientId) =>
        _cluster.GrainFactory.GetGrain<ICareTeamGrain>($"CARE-TEAM:{patientId}");

    private IProviderPatientIndexGrain GetProviderPatientIndex(string providerId) =>
        _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{providerId}");

    private IProviderScheduleIndexGrain GetProviderSchedule(string providerId) =>
        _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>($"PROV-SCHED:{providerId}");

    private IPatientAccessControlGrain GetAccessControl(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");

    /// <summary>
    /// Helper: creates a patient with demographics so the workflow can read name/DOB/SSN.
    /// </summary>
    private async Task SetupPatient(string patientId, string name = "TESTPATIENT,CARETEAM", string ssn = "999001234")
    {
        var patientGrain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patientGrain.UpdateDemographicsAsync(name, "M", new DateTime(1965, 3, 15), ssn);
    }

    // ── Appointment → Auto Care Team ────────────────────────────────────────

    [Test]
    public async Task ScheduleAppointment_WithProvider_AutoAddsCareTeamMember()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        string clinicId = $"CL-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.ScheduleAppointmentAsync(clinicId, "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");

        List<CareTeamMember> members = await GetCareTeam(patientId).GetActiveMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
        Assert.That(members[0].ProviderId, Is.EqualTo(providerId));
        Assert.That(members[0].Role, Is.EqualTo("SPECIALIST"));
        Assert.That(members[0].AssignmentSource, Is.EqualTo("APPOINTMENT"));
    }

    [Test]
    public async Task ScheduleAppointment_WithProvider_AddsToProviderPatientIndex()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        string clinicId = $"CL-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.ScheduleAppointmentAsync(clinicId, "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");

        List<ProviderPatientEntry> patients = await GetProviderPatientIndex(providerId).GetActivePatientsAsync();
        Assert.That(patients, Has.Count.EqualTo(1));
        Assert.That(patients[0].PatientId, Is.EqualTo(patientId));
        Assert.That(patients[0].PatientName, Is.EqualTo("TESTPATIENT,CARETEAM"));
    }

    // ── Clinical action → panel auto-add (orders, notes, consults) ──────────

    [Test]
    public async Task PlaceOrder_AddsPatientToOrderingProviderPanel()
    {
        string patientId = $"PAT-ORD-{Guid.NewGuid()}";
        string providerId = $"PROV-ORD-{Guid.NewGuid()}";
        await SetupPatient(patientId, "ORDERPATIENT,PANEL");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.PlaceOrderAsync("Lab", "CBC WITH DIFFERENTIAL", null,
            providerId, "ORDERING,DOC MD", null, null, "ROUTINE", null, null);

        List<ProviderPatientEntry> panel = await GetProviderPatientIndex(providerId).GetActivePatientsAsync();
        Assert.That(panel.Select(p => p.PatientId), Contains.Item(patientId));
        Assert.That(panel.First(p => p.PatientId == patientId).Relationship, Is.EqualTo("Ordering Provider"));
    }

    [Test]
    public async Task CreateNote_AddsPatientToAuthorPanel()
    {
        string patientId = $"PAT-NOTE-{Guid.NewGuid()}";
        string authorId = $"PROV-NOTE-{Guid.NewGuid()}";
        await SetupPatient(patientId, "NOTEPATIENT,PANEL");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.CreateNoteAsync("PROGRESS NOTE", null, "Seen today.", "Follow-up",
            authorId, "AUTHOR,DOC MD", null, null, null, null, null, DateTime.UtcNow);

        List<ProviderPatientEntry> panel = await GetProviderPatientIndex(authorId).GetActivePatientsAsync();
        Assert.That(panel.Select(p => p.PatientId), Contains.Item(patientId));
    }

    [Test]
    public async Task PlaceOrder_DoesNotOverwriteExistingPcpRelationship()
    {
        string patientId = $"PAT-PCP-{Guid.NewGuid()}";
        string providerId = $"PROV-PCP-{Guid.NewGuid()}";
        await SetupPatient(patientId, "PCPPATIENT,PANEL");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.SetPcpAsync(providerId, "PCP,DOC MD", "Internal Medicine");
        await workflow.PlaceOrderAsync("Lab", "BMP", null, providerId, "PCP,DOC MD",
            null, null, "ROUTINE", null, null);

        List<ProviderPatientEntry> panel = await GetProviderPatientIndex(providerId).GetActivePatientsAsync();
        ProviderPatientEntry entry = panel.First(p => p.PatientId == patientId);
        Assert.That(entry.Relationship, Is.EqualTo("PCP"),
            "an order must not clobber the provider's existing PCP relationship label");
    }

    [Test]
    public async Task ScheduleAppointment_WithProvider_AddsToProviderSchedule()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        string clinicId = $"CL-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        DateTime apptTime = DateTime.UtcNow.AddDays(3);
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.ScheduleAppointmentAsync(clinicId, "PRIMARY CARE",
            apptTime, 30, providerId, "SMITH,JOHN MD", "Follow-up", "REGULAR");

        List<ProviderScheduleEntry> entries = await GetProviderSchedule(providerId).GetAllAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].PatientId, Is.EqualTo(patientId));
        Assert.That(entries[0].Status, Is.EqualTo("Scheduled"));
    }

    [Test]
    public async Task ScheduleAppointment_WithProvider_GrantsAccessToSensitiveRecord()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        string clinicId = $"CL-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        // Mark patient as sensitive
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.SetPatientSensitivityAsync(true, "ELEVATED", new List<string> { "BEHAVIORAL" });

        // Schedule appointment — should auto-grant access
        await workflow.ScheduleAppointmentAsync(clinicId, "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");

        PatientAccessControlState acState = await GetAccessControl(patientId).GetAccessControlAsync();
        Assert.That(acState.AuthorizedProviderIds, Contains.Item(providerId));
    }

    [Test]
    public async Task ScheduleAppointment_NoProvider_NoCareTeamChange()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string clinicId = $"CL-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.ScheduleAppointmentAsync(clinicId, "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, null, null, "Walk-in", "REGULAR");

        List<CareTeamMember> members = await GetCareTeam(patientId).GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(0));
    }

    // ── Appointment Status Changes ──────────────────────────────────────────

    [Test]
    public async Task CheckOut_UpdatesLastSeenDate()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        string apptId = await workflow.ScheduleAppointmentAsync("CL-001", "PRIMARY CARE",
            DateTime.UtcNow.AddHours(-1), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");

        await workflow.CheckInAsync(apptId, null);
        DateTime coTime = DateTime.UtcNow;
        await workflow.CheckOutAsync(apptId, coTime);

        List<CareTeamMember> members = await GetCareTeam(patientId).GetMembersAsync();
        Assert.That(members[0].LastSeenDate, Is.Not.Null);

        List<ProviderPatientEntry> patients = await GetProviderPatientIndex(providerId).GetAllPatientsAsync();
        Assert.That(patients[0].LastSeenDate, Is.Not.Null);
    }

    [Test]
    public async Task CancelAppointment_DoesNotRemoveCareTeamMembership()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        string apptId = await workflow.ScheduleAppointmentAsync("CL-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");
        await workflow.CancelAppointmentAsync(apptId);

        // Care team membership should still be active
        bool isOnTeam = await GetCareTeam(patientId).HasActiveMemberAsync(providerId);
        Assert.That(isOnTeam, Is.True);
    }

    [Test]
    public async Task CancelAppointment_UpdatesProviderScheduleStatus()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        string apptId = await workflow.ScheduleAppointmentAsync("CL-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");
        await workflow.CancelAppointmentAsync(apptId);

        List<ProviderScheduleEntry> entries = await GetProviderSchedule(providerId).GetAllAsync();
        Assert.That(entries[0].Status, Is.EqualTo("Cancelled"));
    }

    // ── Manual Care Team Management ─────────────────────────────────────────

    [Test]
    public async Task ManualAddCareTeamMember_PersistsAndSyncsIndexes()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.AddCareTeamMemberAsync(providerId, "MANUAL,DOC MD",
            "CONSULTING", "NEUROLOGY", "CONSULT", null);

        // Care team grain
        List<CareTeamMember> members = await GetCareTeam(patientId).GetActiveMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
        Assert.That(members[0].AssignmentSource, Is.EqualTo("CONSULT"));

        // Provider patient index
        List<ProviderPatientEntry> patients = await GetProviderPatientIndex(providerId).GetActivePatientsAsync();
        Assert.That(patients, Has.Count.EqualTo(1));

        // Access control
        PatientAccessControlState acState = await GetAccessControl(patientId).GetAccessControlAsync();
        Assert.That(acState.AuthorizedProviderIds, Contains.Item(providerId));
    }

    [Test]
    public async Task RemoveCareTeamMember_DeactivatesNotDeletes()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.AddCareTeamMemberAsync(providerId, "REMOVED,DOC",
            "SPECIALIST", null, "MANUAL", null);
        await workflow.RemoveCareTeamMemberAsync(providerId);

        // Should still exist in members list but as inactive
        List<CareTeamMember> all = await GetCareTeam(patientId).GetMembersAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].IsActive, Is.False);

        // Active list should be empty
        List<CareTeamMember> active = await GetCareTeam(patientId).GetActiveMembersAsync();
        Assert.That(active, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task SetPcp_ReplacesExistingPcp()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.SetPcpAsync("PROV-OLD", "OLD,PCP MD", "FAMILY MEDICINE");
        await workflow.SetPcpAsync("PROV-NEW", "NEW,PCP MD", "INTERNAL MEDICINE");

        CareTeamMember? pcp = await workflow.GetPcpAsync();
        Assert.That(pcp, Is.Not.Null);
        Assert.That(pcp!.ProviderId, Is.EqualTo("PROV-NEW"));
        Assert.That(pcp.Role, Is.EqualTo("PCP"));
    }

    // ── Access Control Integration ──────────────────────────────────────────

    [Test]
    public async Task CheckAccessAsync_GrantedForCareTeamMember()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Mark sensitive and add to care team
        await workflow.SetPatientSensitivityAsync(true, "HIGH", new List<string> { "HIV" });
        await workflow.AddCareTeamMemberAsync(providerId, "TEAM,DOC",
            "SPECIALIST", null, "MANUAL", null);

        bool hasAccess = await workflow.CheckPatientAccessAsync(providerId);
        Assert.That(hasAccess, Is.True);
    }

    [Test]
    public async Task CheckAccessAsync_DeniedForNonMember_SensitiveRecord()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string nonMemberId = $"PROV-RANDOM-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.SetPatientSensitivityAsync(true, "HIGH", new List<string> { "SUBSTANCE_ABUSE" });

        bool hasAccess = await workflow.CheckPatientAccessAsync(nonMemberId);
        Assert.That(hasAccess, Is.False);
    }

    // ── Duplicate Prevention ────────────────────────────────────────────────

    [Test]
    public async Task DuplicateAppointment_DoesNotDuplicateCareTeamEntry()
    {
        string patientId = $"PAT-CT-{Guid.NewGuid()}";
        string providerId = $"PROV-CT-{Guid.NewGuid()}";
        await SetupPatient(patientId);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.ScheduleAppointmentAsync("CL-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(3), 30, providerId, "SMITH,JOHN MD",
            "Follow-up", "REGULAR");
        await workflow.ScheduleAppointmentAsync("CL-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(10), 30, providerId, "SMITH,JOHN MD",
            "Follow-up 2", "FOLLOW-UP");

        List<CareTeamMember> members = await GetCareTeam(patientId).GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
    }
}
