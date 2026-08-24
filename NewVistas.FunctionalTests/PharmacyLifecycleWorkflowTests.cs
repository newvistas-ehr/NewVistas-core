// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the outpatient prescription LIFECYCLE transitions on
/// IPatientWorkflowGrain: Hold → Resume → Discontinue → Expire.
///
/// NOTE: PatientWorkflowGrain formerly carried a SetPendingDurReviewStatusAsync
/// method that was never declared on IPatientWorkflowGrain (unreachable via
/// Orleans) and was a guaranteed no-op; it has been removed.
///
/// Complements PharmacyWorkflowStateMachineTests (which covers the DUR /
/// interaction / verify / fill gates). Here the focus is:
///   1. Each transition's status change is visible on the prescription grain
///      AND on the patient-facing read paths (PSO-INDEX + GetActiveMedicationsAsync)
///      — the cache-coherence class of bug.
///   2. Wrong-state transitions and double-calls: PharmacyGrain deliberately
///      THROWS InvalidOperationException (no silent no-ops), and the failed
///      call must leave state untouched.
///
/// VistA reference: PRESCRIPTION file (#52) status field; PSOORED.m sequencing.
/// </summary>
[TestFixture]
public class PharmacyLifecycleWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPharmacyGrain Rx(string rxId)
        => _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);

    private IPatientPrescriptionIndexGrain Index(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");

    /// <summary>Creates a prescription THROUGH the workflow (PlacePrescriptionAsync),
    /// the same path the UI uses — so the PSO index and patient linkage are exercised.</summary>
    private Task<string> PlaceRxAsync(IPatientWorkflowGrain wf, string drugName = "LIFECYCLE DRUG 10MG")
        => wf.PlacePrescriptionAsync(
            drugName, null, "10mg", "ORAL", "QD", "Take 1 tablet by mouth daily",
            30, 30, 5, "PROV-001", "Dr. Smith", null, null, null);

    private Task PerformPassingDurAsync(IPatientWorkflowGrain wf, string rxId, string drugName = "LIFECYCLE DRUG 10MG")
        => wf.PerformDurAsync(rxId, drugName, null, null,
            "10mg", "ORAL", "QD", 30, 30, null, null,
            false, null, "PHARM-001",
            ingredientIens: new List<string> { $"IEN-LC-{Guid.NewGuid():N}" });

    private async Task<string> IndexStatusOf(string patientId, string rxId)
    {
        List<PrescriptionIndexEntry> all = await Index(patientId).GetAllAsync();
        return all.Single(e => e.PrescriptionId == rxId).Status;
    }

    // ─── Happy lifecycle: place → hold → resume → discontinue ───────────────
    // Every step asserts the grain status AND both patient-facing read paths.

    [Test]
    public async Task Lifecycle_HoldResumeDiscontinue_StatusAndReadPathsTrackEveryTransition()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // ── Place ──
        string rxId = await PlaceRxAsync(wf);

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("ACTIVE"),
            "PSO index reflects creation");
        Assert.That((await wf.GetActiveMedicationsAsync()).Select(m => m.PrescriptionId),
            Does.Contain(rxId), "placed rx appears in the active-med list");
        Assert.That((await Index(patientId).GetActiveAsync()).Select(e => e.PrescriptionId),
            Does.Contain(rxId), "placed rx appears in the index's active view");

        // ── Hold ──
        await wf.HoldPrescriptionWorkflowAsync(rxId, "Awaiting lab results");

        state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("HOLD"));
        Assert.That(state.HoldReason, Is.EqualTo("Awaiting lab results"));
        Assert.That(state.HoldDate, Is.Not.Null);
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("HOLD"),
            "PSO index reflects the hold — not a stale ACTIVE");
        // A held med is still a med the patient is on — it stays in the active-med
        // list (interaction screening must see it) but leaves the index's
        // strictly-ACTIVE view.
        List<MedicationSummary> meds = await wf.GetActiveMedicationsAsync();
        MedicationSummary held = meds.Single(m => m.PrescriptionId == rxId);
        Assert.That(held.Status, Is.EqualTo("HOLD"),
            "active-med list shows the current HOLD status, not a cached ACTIVE");
        Assert.That((await Index(patientId).GetActiveAsync()).Select(e => e.PrescriptionId),
            Does.Not.Contain(rxId), "index active view excludes a held rx");

        // ── Resume ──
        await wf.ResumePrescriptionWorkflowAsync(rxId);

        state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.HoldReason, Is.Null,
            "resume must clear the hold reason — stale hold metadata on an ACTIVE rx misleads");
        Assert.That(state.HoldDate, Is.Null, "resume must clear the hold date");
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("ACTIVE"),
            "PSO index reflects the resume");
        Assert.That((await Index(patientId).GetActiveAsync()).Select(e => e.PrescriptionId),
            Does.Contain(rxId), "resumed rx is back in the index active view");

        // ── Discontinue ──
        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Adverse reaction");

        state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.DiscontinueReason, Is.EqualTo("Adverse reaction"));
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("DISCONTINUED"),
            "PSO index reflects the discontinue — a stale entry here would keep a stopped drug on the med list");
        Assert.That((await wf.GetActiveMedicationsAsync()).Select(m => m.PrescriptionId),
            Does.Not.Contain(rxId), "a discontinued rx must leave the active-med list");
        List<PrescriptionIndexEntry> discontinued = await Index(patientId).GetByStatusAsync("DISCONTINUED");
        Assert.That(discontinued.Select(e => e.PrescriptionId), Does.Contain(rxId));
    }

    // ─── Expiration ─────────────────────────────────────────────────────────

    [Test]
    public async Task Expire_FromActive_LeavesActiveReadPaths()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);

        await wf.ExpirePrescriptionWorkflowAsync(rxId);

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("EXPIRED"));
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("EXPIRED"),
            "PSO index reflects expiration");
        Assert.That((await wf.GetActiveMedicationsAsync()).Select(m => m.PrescriptionId),
            Does.Not.Contain(rxId), "an expired rx must leave the active-med list");
    }

    [Test]
    public async Task Expire_FromHold_IsAllowed()
    {
        // A held prescription can age out — HOLD is a valid pre-expiration state.
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.HoldPrescriptionWorkflowAsync(rxId, "Awaiting clarification");

        await wf.ExpirePrescriptionWorkflowAsync(rxId);

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("EXPIRED"));
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("EXPIRED"));
    }

    [Test]
    public async Task Discontinue_FromHold_IsAllowed()
    {
        // Clinically common: drug is held pending review, review says stop it.
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.HoldPrescriptionWorkflowAsync(rxId, "Pending renal panel");

        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Renal impairment confirmed");

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.DiscontinueReason, Is.EqualTo("Renal impairment confirmed"));
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("DISCONTINUED"));
    }

    // ─── Wrong-state transitions: the grain THROWS, and state is untouched ──

    [Test]
    public async Task Resume_WithoutPriorHold_Throws_AndStaysActive()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.ResumePrescriptionWorkflowAsync(rxId));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"), "failed resume must not disturb an active rx");
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task Resume_AfterDiscontinue_Throws_AndDoesNotReactivate()
    {
        // The dangerous variant: a silent "resume" of a discontinued drug would
        // put a stopped medication back on the patient's med list.
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Stopped by provider");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.ResumePrescriptionWorkflowAsync(rxId));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That((await wf.GetActiveMedicationsAsync()).Select(m => m.PrescriptionId),
            Does.Not.Contain(rxId), "a discontinued rx must never sneak back into the active-med list");
    }

    [Test]
    public async Task Hold_AfterDiscontinue_Throws_AndStateUnchanged()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "No longer indicated");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.HoldPrescriptionWorkflowAsync(rxId, "too late"));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.HoldReason, Is.Null, "failed hold must not stamp a hold reason");
        Assert.That(state.HoldDate, Is.Null);
        Assert.That(await IndexStatusOf(patientId, rxId), Is.EqualTo("DISCONTINUED"));
    }

    [Test]
    public async Task Hold_AfterExpire_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.ExpirePrescriptionWorkflowAsync(rxId);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.HoldPrescriptionWorkflowAsync(rxId, "cannot hold expired"));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("EXPIRED"));
    }

    [Test]
    public async Task Expire_AfterDiscontinue_Throws()
    {
        // DISCONTINUED is terminal-with-reason; recasting it as EXPIRED would
        // erase why the drug was stopped.
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Therapy complete");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.ExpirePrescriptionWorkflowAsync(rxId));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.DiscontinueReason, Is.EqualTo("Therapy complete"));
    }

    // ─── Double-calls: deliberately NOT idempotent — the second call throws ─

    [Test]
    public async Task Hold_Twice_SecondCallThrows_AndOriginalHoldReasonSurvives()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);

        await wf.HoldPrescriptionWorkflowAsync(rxId, "First reason");
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.HoldPrescriptionWorkflowAsync(rxId, "Second reason"));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("HOLD"));
        Assert.That(state.HoldReason, Is.EqualTo("First reason"),
            "a rejected duplicate hold must not overwrite the original reason");
    }

    [Test]
    public async Task Resume_Twice_SecondCallThrows()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await wf.HoldPrescriptionWorkflowAsync(rxId, "Brief hold");

        await wf.ResumePrescriptionWorkflowAsync(rxId);
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.ResumePrescriptionWorkflowAsync(rxId));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task Discontinue_Twice_SecondCallThrows_AndOriginalReasonSurvives()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);

        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Original reason");
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Overwriting reason"));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.DiscontinueReason, Is.EqualTo("Original reason"),
            "a rejected duplicate discontinue must not overwrite the audit reason");
    }

    [Test]
    public async Task Expire_Twice_SecondCallThrows()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);

        await wf.ExpirePrescriptionWorkflowAsync(rxId);
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.ExpirePrescriptionWorkflowAsync(rxId));

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("EXPIRED"));
    }

    // ─── Lifecycle × fill interplay ─────────────────────────────────────────

    [Test]
    public async Task Fill_BlockedWhileOnHold_SucceedsAfterResume()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);

        // Clear all fill gates first (DUR passed, verified) so HOLD is the ONLY blocker.
        await PerformPassingDurAsync(wf, rxId);
        await wf.VerifyPrescriptionWorkflowAsync(rxId, "RPH-001");

        await wf.HoldPrescriptionWorkflowAsync(rxId, "Patient hospitalized");
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow),
            "a held prescription must not be fillable");

        PharmacyState state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.Null, "the blocked fill must not have recorded a fill");

        await wf.ResumePrescriptionWorkflowAsync(rxId);
        await wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow);

        state = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.Not.Null, "after resume the same fill goes through");
        Assert.That(state.RefillHistory, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RefillEligibility_ReportsHoldAndDiscontinueStatus()
    {
        // The eligibility read path must see lifecycle transitions too.
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await PlaceRxAsync(wf);
        await PerformPassingDurAsync(wf, rxId);
        await wf.VerifyPrescriptionWorkflowAsync(rxId, "RPH-001");
        await wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow.AddDays(-25));

        await wf.HoldPrescriptionWorkflowAsync(rxId, "Hold for review");
        RefillEligibilityResult onHold = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);
        Assert.That(onHold.IsEligible, Is.False);
        Assert.That(onHold.Status, Is.EqualTo("HOLD"));

        await wf.ResumePrescriptionWorkflowAsync(rxId);
        await wf.DiscontinuePrescriptionWorkflowAsync(rxId, "Course finished");
        RefillEligibilityResult afterDc = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);
        Assert.That(afterDc.IsEligible, Is.False);
        Assert.That(afterDc.Status, Is.EqualTo("DISCONTINUED"));
    }

}
