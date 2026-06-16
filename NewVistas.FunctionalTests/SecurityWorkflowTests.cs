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
/// Functional tests for VistA Patient Access Control — DG SENSITIVITY (File #38.1).
/// Covers sensitive record flags, authorized provider lists, break-the-glass audit trail,
/// and workflow grain delegation.
/// </summary>
[TestFixture]
public class SecurityWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientAccessControlGrain NewPacGrain()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        return _cluster.GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── 1. Default State ──────────────────────────────────────────────────

    [Test]
    public async Task PatientAccess_DefaultNotSensitive()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        PatientAccessControlState state = await grain.GetAccessControlAsync();

        Assert.That(state.IsSensitive, Is.False);
        Assert.That(state.SensitivityLevel, Is.EqualTo(string.Empty));
        Assert.That(state.SensitivityCategories, Has.Count.EqualTo(0));
        Assert.That(state.AuthorizedProviderIds, Has.Count.EqualTo(0));
        Assert.That(state.AccessLog, Has.Count.EqualTo(0));
    }

    // ─── 2. Set Sensitivity ────────────────────────────────────────────────

    [Test]
    public async Task PatientAccess_SetSensitivity()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.SetSensitivityAsync(true, "ELEVATED", new List<string> { "HIV", "BEHAVIORAL" });

        PatientAccessControlState state = await grain.GetAccessControlAsync();
        Assert.That(state.IsSensitive, Is.True);
        Assert.That(state.SensitivityLevel, Is.EqualTo("ELEVATED"));
        Assert.That(state.SensitivityCategories, Has.Count.EqualTo(2));
        Assert.That(state.SensitivityCategories, Contains.Item("HIV"));
        Assert.That(state.SensitivityCategories, Contains.Item("BEHAVIORAL"));
    }

    // ─── 3. Add Authorized Provider ────────────────────────────────────────

    [Test]
    public async Task PatientAccess_AddAuthorizedProvider()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.AddAuthorizedProviderAsync("PROV-001");
        await grain.AddAuthorizedProviderAsync("PROV-002");

        PatientAccessControlState state = await grain.GetAccessControlAsync();
        Assert.That(state.AuthorizedProviderIds, Has.Count.EqualTo(2));
        Assert.That(state.AuthorizedProviderIds, Contains.Item("PROV-001"));
        Assert.That(state.AuthorizedProviderIds, Contains.Item("PROV-002"));
    }

    // ─── 4. Remove Authorized Provider ─────────────────────────────────────

    [Test]
    public async Task PatientAccess_RemoveAuthorizedProvider()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.AddAuthorizedProviderAsync("PROV-001");
        await grain.AddAuthorizedProviderAsync("PROV-002");
        await grain.RemoveAuthorizedProviderAsync("PROV-001");

        PatientAccessControlState state = await grain.GetAccessControlAsync();
        Assert.That(state.AuthorizedProviderIds, Has.Count.EqualTo(1));
        Assert.That(state.AuthorizedProviderIds, Contains.Item("PROV-002"));
    }

    // ─── 5. Check Access Non-Sensitive ─────────────────────────────────────

    [Test]
    public async Task PatientAccess_CheckAccessNonSensitive_ReturnsTrue()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        bool hasAccess = await grain.CheckAccessAsync("ANY-USER");

        Assert.That(hasAccess, Is.True);
    }

    // ─── 6. Check Access Sensitive Not Authorized ──────────────────────────

    [Test]
    public async Task PatientAccess_CheckAccessSensitive_NotAuthorized_ReturnsFalse()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.SetSensitivityAsync(true, "HIGH", new List<string> { "EMPLOYEE" });

        bool hasAccess = await grain.CheckAccessAsync("UNAUTHORIZED-USER");

        Assert.That(hasAccess, Is.False);
    }

    // ─── 7. Check Access Sensitive Authorized ──────────────────────────────

    [Test]
    public async Task PatientAccess_CheckAccessSensitive_Authorized_ReturnsTrue()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.SetSensitivityAsync(true, "ELEVATED", new List<string> { "BEHAVIORAL" });
        await grain.AddAuthorizedProviderAsync("PROV-AUTHORIZED");

        bool hasAccess = await grain.CheckAccessAsync("PROV-AUTHORIZED");

        Assert.That(hasAccess, Is.True);
    }

    // ─── 8. Record Access ──────────────────────────────────────────────────

    [Test]
    public async Task PatientAccess_RecordAccess()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.RecordAccessAsync("USER-001", "Dr. Smith", "TREATING_PROVIDER", false, null);

        List<PatientAccessLog> log = await grain.GetAccessLogAsync();
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].UserId, Is.EqualTo("USER-001"));
        Assert.That(log[0].UserName, Is.EqualTo("Dr. Smith"));
        Assert.That(log[0].AccessReason, Is.EqualTo("TREATING_PROVIDER"));
        Assert.That(log[0].WasBreakTheGlass, Is.False);
    }

    // ─── 9. Record Break-the-Glass ─────────────────────────────────────────

    [Test]
    public async Task PatientAccess_RecordBreakTheGlass()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.RecordAccessAsync("USER-002", "Dr. Chen", "BREAK_THE_GLASS", true,
            "Emergency consult for acute symptoms");

        List<PatientAccessLog> log = await grain.GetAccessLogAsync();
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].WasBreakTheGlass, Is.True);
        Assert.That(log[0].JustificationText, Is.EqualTo("Emergency consult for acute symptoms"));
        Assert.That(log[0].AccessReason, Is.EqualTo("BREAK_THE_GLASS"));
    }

    // ─── 10. Get Access Log ────────────────────────────────────────────────

    [Test]
    public async Task PatientAccess_GetAccessLog()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.RecordAccessAsync("USER-A", "Alice", "TREATING_PROVIDER", false, null);
        await grain.RecordAccessAsync("USER-B", "Bob", "ADMINISTRATIVE", false, null);
        await grain.RecordAccessAsync("USER-C", "Carol", "BREAK_THE_GLASS", true, "Emergency");

        List<PatientAccessLog> log = await grain.GetAccessLogAsync();
        Assert.That(log, Has.Count.EqualTo(3));
        Assert.That(log[2].WasBreakTheGlass, Is.True);
    }

    // ─── 11. Clear Access Log ──────────────────────────────────────────────

    [Test]
    public async Task PatientAccess_ClearAccessLog()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.RecordAccessAsync("USER-A", "Alice", "TREATING_PROVIDER", false, null);
        await grain.RecordAccessAsync("USER-B", "Bob", "ADMINISTRATIVE", false, null);
        await grain.ClearAccessLogAsync();

        List<PatientAccessLog> log = await grain.GetAccessLogAsync();
        Assert.That(log, Has.Count.EqualTo(0));
    }

    // ─── 12. Multiple Sensitivity Categories ───────────────────────────────

    [Test]
    public async Task PatientAccess_MultipleSensitivityCategories()
    {
        IPatientAccessControlGrain grain = NewPacGrain();

        await grain.SetSensitivityAsync(true, "HIGH",
            new List<string> { "HIV", "SICKLE_CELL", "SUBSTANCE_ABUSE", "BEHAVIORAL", "EMPLOYEE" });

        PatientAccessControlState state = await grain.GetAccessControlAsync();
        Assert.That(state.SensitivityCategories, Has.Count.EqualTo(5));
        Assert.That(state.SensitivityCategories, Contains.Item("HIV"));
        Assert.That(state.SensitivityCategories, Contains.Item("SICKLE_CELL"));
        Assert.That(state.SensitivityCategories, Contains.Item("SUBSTANCE_ABUSE"));
        Assert.That(state.SensitivityCategories, Contains.Item("BEHAVIORAL"));
        Assert.That(state.SensitivityCategories, Contains.Item("EMPLOYEE"));
    }

    // ─── 13. Workflow Set Sensitivity ──────────────────────────────────────

    [Test]
    public async Task PatientAccess_WorkflowSetSensitivity()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.SetPatientSensitivityAsync(true, "ELEVATED", new List<string> { "BEHAVIORAL" });

        // Verify via PAC grain
        IPatientAccessControlGrain pacGrain = _cluster.GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");
        PatientAccessControlState state = await pacGrain.GetAccessControlAsync();
        Assert.That(state.IsSensitive, Is.True);
        Assert.That(state.SensitivityLevel, Is.EqualTo("ELEVATED"));

        // Verify patient grain has mirror flags
        IPatientGrain patientGrain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        PatientState patientState = await patientGrain.GetPatientAsync();
        Assert.That(patientState.IsSensitiveRecord, Is.True);
        Assert.That(patientState.SensitivityLevel, Is.EqualTo("ELEVATED"));
    }

    // ─── 14. Workflow Check Access ─────────────────────────────────────────

    [Test]
    public async Task PatientAccess_WorkflowCheckAccess()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Not sensitive → anyone has access
        bool accessBefore = await workflow.CheckPatientAccessAsync("RANDOM-USER");
        Assert.That(accessBefore, Is.True);

        // Make sensitive
        await workflow.SetPatientSensitivityAsync(true, "HIGH", new List<string> { "EMPLOYEE" });

        // Unauthorized → no access
        bool accessAfter = await workflow.CheckPatientAccessAsync("RANDOM-USER");
        Assert.That(accessAfter, Is.False);

        // Add authorized → access
        await workflow.AddAuthorizedProviderAsync("RANDOM-USER");
        bool accessAuthorized = await workflow.CheckPatientAccessAsync("RANDOM-USER");
        Assert.That(accessAuthorized, Is.True);
    }

    // ─── 15. Workflow Record BTG ───────────────────────────────────────────

    [Test]
    public async Task PatientAccess_WorkflowRecordBtg()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.RecordPatientAccessAsync(
            "USER-BTG", "Dr. Emergency", "BREAK_THE_GLASS", true, "Acute emergency override");

        List<PatientAccessLog> log = await workflow.GetPatientAccessLogAsync();
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].WasBreakTheGlass, Is.True);
        Assert.That(log[0].JustificationText, Is.EqualTo("Acute emergency override"));
    }

    // ─── 16. Workflow Get Access Log ───────────────────────────────────────

    [Test]
    public async Task PatientAccess_WorkflowGetAccessLog()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.RecordPatientAccessAsync("USER-1", "Alice", "TREATING_PROVIDER", false, null);
        await workflow.RecordPatientAccessAsync("USER-2", "Bob", "EMERGENCY", false, null);
        await workflow.RecordPatientAccessAsync("USER-3", "Carol", "BREAK_THE_GLASS", true, "Override justification");

        List<PatientAccessLog> log = await workflow.GetPatientAccessLogAsync();
        Assert.That(log, Has.Count.EqualTo(3));
        Assert.That(log[0].UserId, Is.EqualTo("USER-1"));
        Assert.That(log[1].AccessReason, Is.EqualTo("EMERGENCY"));
        Assert.That(log[2].WasBreakTheGlass, Is.True);
    }
}
