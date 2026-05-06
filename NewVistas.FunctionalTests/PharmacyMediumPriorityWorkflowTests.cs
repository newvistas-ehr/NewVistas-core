// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for medium-priority pharmacy gaps via IPatientWorkflowGrain:
/// 1. NDC/Lot Tracking at Dispense
/// 2. Patient Counseling Workflow
/// 3. Label Generation (with patient name enrichment)
/// 4. DUR-blocked prescriptions show in pending review
///
/// VistA reference: PSO dispense recording, PSOCP.m, PSJLBL.m, PSOORED.m incomplete.
/// </summary>
[TestFixture]
public class PharmacyMediumPriorityWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> CreateVerifiedFilledRxWithDur(string patientId, int daysAgoFilled = 5)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "METFORMIN 500MG", null,
            "500mg", "ORAL", "BID", "Take one tablet twice daily with meals",
            30, 60, 5, "PROV-001", "Dr. Smith", "PHARM-001", "Main Pharmacy", null, null);

        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.PerformDurAsync(rxId, "METFORMIN 500MG", null, null,
            "500mg", "ORAL", "BID", 30, 60, null, null,
            false, null, "PHARM-001",
            ingredientIens: new List<string> { "IEN-METFORMIN" });

        await rx.VerifyAsync("RPH-001");
        await wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow.Date.AddDays(-daysAgoFilled));
        return rxId;
    }

    // ═══ NDC/LOT TRACKING VIA WORKFLOW ══════════════════════════════════════

    [Test]
    public async Task RecordDispenseWorkflow_PersistsOnGrain()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRxWithDur(patientId);

        await wf.RecordDispenseWorkflowAsync(rxId, "00378-1805-01", "LOT-2026-A001", "RPH-002");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.NdcDispensed, Is.EqualTo("00378-1805-01"));
        Assert.That(state.LotNumber, Is.EqualTo("LOT-2026-A001"));
    }

    [Test]
    public async Task RecordDispenseWorkflow_UpdatesRefillHistory()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRxWithDur(patientId);

        await wf.RecordDispenseWorkflowAsync(rxId, "55111-0123-01", "LOT-B002", "RPH-003");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        List<RefillRecord> history = await rx.GetRefillHistoryAsync();
        Assert.That(history[^1].NdcDispensed, Is.EqualTo("55111-0123-01"));
        Assert.That(history[^1].PharmacistId, Is.EqualTo("RPH-003"));
    }

    // ═══ PATIENT COUNSELING VIA WORKFLOW ════════════════════════════════════

    [Test]
    public async Task RecordCounselingWorkflow_CompletesSession()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRxWithDur(patientId);

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.SetCounselingFlagAsync(true);

        await wf.RecordCounselingWorkflowAsync(rxId, "RPH-004",
            "Discussed proper use, storage, side effects, and when to contact provider.");

        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.CounselingCompleted, Is.True);
        Assert.That(state.CounseledBy, Is.EqualTo("RPH-004"));
        Assert.That(state.CounselingNotes, Does.Contain("side effects"));
    }

    [Test]
    public async Task RecordCounselingWorkflow_NotRequired_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRxWithDur(patientId);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.RecordCounselingWorkflowAsync(rxId, "RPH-001", "Notes"));
    }

    // ═══ LABEL GENERATION VIA WORKFLOW ══════════════════════════════════════

    [Test]
    public async Task GenerateLabelWorkflow_IncludesPatientName()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Create Rx with DUR first (before setting DOB which triggers age warning)
        string rxId = await CreateVerifiedFilledRxWithDur(patientId);

        // Set patient name after DUR (so age-based warning doesn't make DUR Pending)
        await wf.UpdateDemographicsAsync("SMITH, JOHN Q", "M", new DateTime(1960, 5, 15), null);

        PrescriptionLabelContent label = await wf.GenerateLabelContentWorkflowAsync(rxId);

        Assert.That(label.PatientName, Is.EqualTo("SMITH, JOHN Q"));
        Assert.That(label.DrugName, Is.EqualTo("METFORMIN 500MG"));
        Assert.That(label.Sig, Is.EqualTo("Take one tablet twice daily with meals"));
        Assert.That(label.BarcodeData, Is.Not.Empty);
    }

    [Test]
    public async Task GenerateLabelWorkflow_NotVerified_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "DRUG", null, null, null, null, null,
            30, 30, 5, null, null, null, null, null, null);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.GenerateLabelContentWorkflowAsync(rxId));
    }

    // ═══ DUR PENDING STATUS ═════════════════════════════════════════════════

    [Test]
    public async Task DurFailed_PrescriptionBlockedFromFill()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Create Rx and perform DUR that fails (allergy match)
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "PENICILLIN VK 500MG", null,
            "500mg", "ORAL", "QID", null, 10, 40, 3, null, null, null, null, null, null);

        await wf.RecordAllergyAsync("PENICILLIN", "DRUG", null, "OBSERVED",
            new List<string> { "RASH" }, "MODERATE", null, null, null);

        string durId = await wf.PerformDurAsync(rxId, "PENICILLIN VK 500MG", null, null,
            "500mg", "ORAL", "QID", 10, 40, null, null,
            false, null, "PHARM-001");

        // DUR should be in pending/failed review queue
        List<DurAssessmentIndexEntry> pending = await wf.GetPendingDurReviewsAsync();
        Assert.That(pending, Has.Some.Matches<DurAssessmentIndexEntry>(e => e.AssessmentId == durId));

        // DUR not cleared
        Assert.That(await wf.IsDurClearedForPrescriptionAsync(rxId), Is.False);

        // Fill should be blocked
        await rx.VerifyAsync("RPH-001");
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task DurOverridden_PrescriptionUnblockedForFill()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "SULFA DRUG 250MG", null,
            "250mg", "ORAL", "BID", null, 10, 20, 3, null, null, null, null, null, null);

        await wf.RecordAllergyAsync("SULFA", "DRUG CLASS", null, "OBSERVED",
            new List<string> { "NAUSEA" }, "MILD", null, null, null);

        string durId = await wf.PerformDurAsync(rxId, "SULFA DRUG 250MG", null, null,
            "250mg", "ORAL", "BID", 10, 20, null, null,
            false, null, "PHARM-001");

        // Override the allergy check
        await wf.OverrideDurCheckAsync(durId, DurCheckType.DrugAllergyContraindication,
            "PHARM-SENIOR", "Patient tolerates this sulfa drug.");

        // Acknowledge
        await wf.AcknowledgeDurAsync(durId, "PHARM-SENIOR", "Reviewed.");

        // Now DUR should be cleared
        Assert.That(await wf.IsDurClearedForPrescriptionAsync(rxId), Is.True);

        // Fill should succeed
        await rx.VerifyAsync("RPH-001");
        Assert.DoesNotThrowAsync(() => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }
}
