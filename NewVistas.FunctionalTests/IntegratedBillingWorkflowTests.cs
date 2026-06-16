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
/// Functional tests for VistA Integrated Billing — Files #350–354, #355.x.
/// Covers billing actions, patient copay accounts, insurance plans, and personal policies.
/// </summary>
[TestFixture]
public class IntegratedBillingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IIBillingActionGrain NewAction()
        => _cluster.GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{Guid.NewGuid()}");

    private IIBillingActionGrain GetAction(string id)
        => _cluster.GrainFactory.GetGrain<IIBillingActionGrain>(id);

    private IIBillingActionIndexGrain GetActionIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IIBillingActionIndexGrain>($"IB-ACTION-IDX:{patientId}");

    private IIBillingPatientGrain GetBillingPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IIBillingPatientGrain>($"IB-PATIENT:{patientId}");

    private IInsurancePlanGrain GetInsurancePlan(string planId)
        => _cluster.GrainFactory.GetGrain<IInsurancePlanGrain>($"IB-PLAN:{planId}");

    private IInsurancePlanIndexGrain GetPlanIndex()
        => _cluster.GrainFactory.GetGrain<IInsurancePlanIndexGrain>("IB-PLAN-INDEX");

    private IPersonalPolicyGrain GetPersonalPolicy(string policyId)
        => _cluster.GrainFactory.GetGrain<IPersonalPolicyGrain>($"IB-POLICY:{policyId}");

    private IPersonalPolicyIndexGrain GetPolicyIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IPersonalPolicyIndexGrain>($"IB-POLICY-IDX:{patientId}");

    // ─── Billing Action Creation ──────────────────────────────────────────

    [Test]
    public async Task CreateBillingAction_SetsIncompleteStatus()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-001", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 15m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.Status, Is.EqualTo(IBillingActionStatus.Incomplete));
    }

    [Test]
    public async Task CreateBillingAction_ReturnsNonEmptyId()
    {
        IIBillingActionGrain action = NewAction();
        string id = await action.CreateAsync(
            "PAT-IB-002", "COPAY-RX", "Pharmacy Copay",
            IBActionCategory.Pharmacy, 8m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, "RX-001", null);

        Assert.That(id, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task CreateBillingAction_StoresChargeAmount()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-003", "COPAY-IP", "Inpatient Copay",
            IBActionCategory.Inpatient, 50m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.ChargeAmount, Is.EqualTo(50m));
        Assert.That(state.ActionCategory, Is.EqualTo(IBActionCategory.Inpatient));
    }

    [Test]
    public async Task CreateBillingAction_StoresPatientAndUser()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-004", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 15m,
            DateTime.UtcNow, "USR-005", "Dr. Jones",
            "ENC-001", "J11.1", "99213", "LOC-001", null, null, "Test visit");

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-IB-004"));
        Assert.That(state.EnteredByUserId, Is.EqualTo("USR-005"));
        Assert.That(state.DiagnosisCode, Is.EqualTo("J11.1"));
    }

    // ─── Billing Action Status Updates ───────────────────────────────────

    [Test]
    public async Task UpdateStatus_ChangesStatus()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-010", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 15m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        await action.UpdateStatusAsync(IBillingActionStatus.CompletePendingAR);

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.Status, Is.EqualTo(IBillingActionStatus.CompletePendingAR));
    }

    [Test]
    public async Task UpdateStatus_ToBilled_SetsBilledStatus()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-011", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 15m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        await action.UpdateStatusAsync(IBillingActionStatus.Billed);

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.Status, Is.EqualTo(IBillingActionStatus.Billed));
    }

    // ─── Cancellation ────────────────────────────────────────────────────

    [Test]
    public async Task CancelBillingAction_SetsCancelledStatus()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-020", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 15m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        await action.CancelAsync("MEANS_TEST", "Patient not eligible", "USR-002");

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.Status, Is.EqualTo(IBillingActionStatus.Cancelled));
        Assert.That(state.RemoveReasonCode, Is.EqualTo("MEANS_TEST"));
    }

    // ─── Copay Exemption ─────────────────────────────────────────────────

    [Test]
    public async Task SetExempt_SetsIsExemptAndReason()
    {
        IIBillingActionGrain action = NewAction();
        await action.CreateAsync(
            "PAT-IB-030", "COPAY-OP", "Outpatient Copay",
            IBActionCategory.Outpatient, 15m,
            DateTime.UtcNow, "USR-001", "Test User",
            null, null, null, null, null, null, null);

        await action.SetExemptAsync("SC_EXEMPT");

        IBillingActionState state = await action.GetAsync();
        Assert.That(state.IsExempt, Is.True);
        Assert.That(state.ExemptionReason, Is.EqualTo("SC_EXEMPT"));
    }

    // ─── Billing Patient (Copay Account) ─────────────────────────────────

    [Test]
    public async Task EnsureInitialized_CreatesPatientRecord()
    {
        string patientId = $"PAT-IB-INIT-{Guid.NewGuid()}";
        IIBillingPatientGrain bp = GetBillingPatient(patientId);

        await bp.EnsureInitializedAsync(patientId);

        IBillingPatientState state = await bp.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.IsExemptFromCopay, Is.False);
    }

    [Test]
    public async Task EnsureInitialized_IsIdempotent()
    {
        string patientId = $"PAT-IB-IDEM-{Guid.NewGuid()}";
        IIBillingPatientGrain bp = GetBillingPatient(patientId);

        await bp.EnsureInitializedAsync(patientId);
        await bp.EnsureInitializedAsync(patientId); // second call should not throw

        IBillingPatientState state = await bp.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task SetCopayExemption_SetsExemptFlag()
    {
        string patientId = $"PAT-IB-EXEMPT-{Guid.NewGuid()}";
        IIBillingPatientGrain bp = GetBillingPatient(patientId);
        await bp.EnsureInitializedAsync(patientId);

        await bp.SetCopayExemptionAsync(true, "SC", DateTime.UtcNow, null);

        IBillingPatientState state = await bp.GetAsync();
        Assert.That(state.IsExemptFromCopay, Is.True);
        Assert.That(state.ExemptionReasonCode, Is.EqualTo("SC"));
    }

    [Test]
    public async Task GrantHardship_SetsHardshipFlag()
    {
        string patientId = $"PAT-IB-HS-{Guid.NewGuid()}";
        IIBillingPatientGrain bp = GetBillingPatient(patientId);
        await bp.EnsureInitializedAsync(patientId);

        await bp.GrantHardshipAsync(DateTime.UtcNow, null);

        IBillingPatientState state = await bp.GetAsync();
        Assert.That(state.IsHardshipGranted, Is.True);
    }

    [Test]
    public async Task AddCopayTransaction_IncrementsYearToDateBalance()
    {
        string patientId = $"PAT-IB-YTD-{Guid.NewGuid()}";
        IIBillingPatientGrain bp = GetBillingPatient(patientId);
        await bp.EnsureInitializedAsync(patientId);

        await bp.AddCopayTransactionAsync("IB-ACT-001", "Outpatient Copay", 15m, DateTime.UtcNow, false);
        await bp.AddCopayTransactionAsync("IB-ACT-002", "Pharmacy Copay", 8m, DateTime.UtcNow, false);

        IBillingPatientState state = await bp.GetAsync();
        Assert.That(state.CurrentYearCopayBalance, Is.EqualTo(23m));
        Assert.That(state.YearToDateCopayTransactions, Has.Count.EqualTo(2));
    }

    // ─── Billing Action Index ─────────────────────────────────────────────

    [Test]
    public async Task BillingActionIndex_AddOrUpdate_AppearsInGetAll()
    {
        string patientId = $"PAT-IB-IDX-{Guid.NewGuid()}";
        IIBillingActionIndexGrain index = GetActionIndex(patientId);

        string actionId = $"IB-ACTION-{Guid.NewGuid()}";
        IBillingActionIndexEntry entry = new IBillingActionIndexEntry
        {
            BillingActionId = actionId,
            PatientId = patientId,
            ActionTypeCode = "COPAY-OP",
            ActionTypeDescription = "Outpatient Copay",
            ChargeAmount = 15m,
            ServiceDate = DateTime.UtcNow,
            Status = IBillingActionStatus.Incomplete
        };

        await index.AddOrUpdateAsync(entry);
        List<IBillingActionIndexEntry> all = await index.GetAllAsync();

        Assert.That(all.Any(e => e.BillingActionId == actionId), Is.True);
    }
}
