// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Dental — Files #228, #228.1.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class DentalWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Dental Patient record ────────────────────────────────────────────────

    [Test]
    public async Task GetDentalPatient_NewPatient_ReturnsInitialisedRecord()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        DentalPatientState record = await Workflow(patientId).GetDentalPatientAsync();

        Assert.That(record.PatientId, Is.EqualTo(patientId));
        Assert.That(record.EligibilityStatus, Is.EqualTo(DentalEligibilityStatus.Unknown));
    }

    [Test]
    public async Task UpdateDentalEligibility_PersistsStatusAndBasis()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDentalEligibilityAsync(
            DentalEligibilityStatus.Eligible,
            "SC",
            "Service-Connected, 50%+ rating");

        DentalPatientState record = await wf.GetDentalPatientAsync();

        Assert.That(record.EligibilityStatus, Is.EqualTo(DentalEligibilityStatus.Eligible));
        Assert.That(record.EligibilityBasisCode, Is.EqualTo("SC"));
        Assert.That(record.EligibilityBasisDescription, Does.Contain("50%"));
    }

    [Test]
    public async Task SetPrimaryDentist_PersistsDentistInfo()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetPrimaryDentistAsync("DENT-100", "Dr. Mary Chen");

        DentalPatientState record = await wf.GetDentalPatientAsync();

        Assert.That(record.PrimaryDentistId, Is.EqualTo("DENT-100"));
        Assert.That(record.PrimaryDentistName, Is.EqualTo("Dr. Mary Chen"));
    }

    [Test]
    public async Task UpdateDentalClinicalStatus_PersistsAllFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDentalClinicalStatusAsync(
            DentalPeriodontalStatus.PeriodontitisModerateGeneralized,
            "Full lower denture",
            12,
            false,
            "Heavy calculus build-up noted.");

        DentalPatientState record = await wf.GetDentalPatientAsync();

        Assert.That(record.PeriodontalStatus,
            Is.EqualTo(DentalPeriodontalStatus.PeriodontitisModerateGeneralized));
        Assert.That(record.ProstheticStatus, Is.EqualTo("Full lower denture"));
        Assert.That(record.RemainingTeethCount, Is.EqualTo(12));
        Assert.That(record.OnFluoride, Is.False);
        Assert.That(record.ClinicalNotes, Does.Contain("calculus"));
    }

    [Test]
    public async Task RecordDentalVisitDates_UpdatesCorrectFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime examDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        DateTime xrayDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        await wf.RecordDentalVisitDatesAsync(examDate, xrayDate, null);

        DentalPatientState record = await wf.GetDentalPatientAsync();

        Assert.That(record.LastExamDate, Is.EqualTo(examDate));
        Assert.That(record.LastXRayDate, Is.EqualTo(xrayDate));
        Assert.That(record.LastCleaningDate, Is.Null);
    }

    // ─── Dental Treatment workflow ────────────────────────────────────────────

    [Test]
    public async Task RecordDentalTreatment_ReturnsNewTreatmentId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        string treatmentId = await Workflow(patientId).RecordDentalTreatmentAsync(
            DateTime.UtcNow,
            "D2140",
            "Amalgam Restoration, One Surface, Permanent",
            DentalProcedureCategory.Restorative,
            new List<int> { 19 },
            new List<string> { "O" },
            "PROV-001", "Dr. Smith",
            null, null,
            "K02.51", "Local",
            95.00m,
            null);

        Assert.That(treatmentId, Is.Not.Null.And.Not.Empty);
        Assert.That(treatmentId, Does.StartWith("DENTAL-TX:"));
    }

    [Test]
    public async Task RecordDentalTreatment_AppearsInTreatmentIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.RecordDentalTreatmentAsync(
            DateTime.UtcNow,
            "D1110",
            "Prophylaxis – Adult",
            DentalProcedureCategory.Preventive,
            new List<int>(),
            new List<string>(),
            "PROV-001", "Dr. Jones",
            null, null, null, null, 75.00m, null);

        List<DentalTreatmentIndexEntry> treatments = await wf.GetDentalTreatmentsAsync();

        Assert.That(treatments, Has.Count.EqualTo(1));
        Assert.That(treatments[0].ProcedureCode, Is.EqualTo("D1110"));
        Assert.That(treatments[0].Status, Is.EqualTo(DentalTreatmentStatus.Planned));
    }

    [Test]
    public async Task CompleteDentalTreatment_UpdatesStatusInIndexAndGrain()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string treatmentId = await wf.RecordDentalTreatmentAsync(
            DateTime.UtcNow,
            "D2160",
            "Amalgam Restoration, Three Surfaces, Permanent",
            DentalProcedureCategory.Restorative,
            new List<int> { 30 },
            new List<string> { "M", "O", "D" },
            "PROV-001", "Dr. Smith",
            null, null, null, "Local", 175.00m, null);

        DateTime completedDate = DateTime.UtcNow;
        await wf.CompleteDentalTreatmentAsync(treatmentId, completedDate, "USR-001", "No complications.");

        // Check index entry
        List<DentalTreatmentIndexEntry> treatments = await wf.GetDentalTreatmentsAsync();
        Assert.That(treatments[0].Status, Is.EqualTo(DentalTreatmentStatus.Completed));

        // Check treatment grain directly
        DentalTreatmentState state = await wf.GetDentalTreatmentAsync(treatmentId);
        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Completed));
        Assert.That(state.CompletedDate, Is.EqualTo(completedDate).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task CancelDentalTreatment_UpdatesStatusInIndexAndGrain()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string treatmentId = await wf.RecordDentalTreatmentAsync(
            DateTime.UtcNow,
            "D3330",
            "Endodontic Therapy, Molar Tooth",
            DentalProcedureCategory.Endodontic,
            new List<int> { 14 },
            new List<string>(),
            "PROV-001", "Dr. Patel",
            null, null, "K04.0", "Local", 850.00m, null);

        await wf.CancelDentalTreatmentAsync(treatmentId, "Patient transferred to private care", "USR-005");

        List<DentalTreatmentIndexEntry> treatments = await wf.GetDentalTreatmentsAsync();
        Assert.That(treatments[0].Status, Is.EqualTo(DentalTreatmentStatus.Cancelled));

        DentalTreatmentState state = await wf.GetDentalTreatmentAsync(treatmentId);
        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Cancelled));
        Assert.That(state.StatusReason, Does.Contain("private care"));
    }

    [Test]
    public async Task ReferDentalTreatment_UpdatesStatusInIndexAndGrain()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string treatmentId = await wf.RecordDentalTreatmentAsync(
            DateTime.UtcNow,
            "D7240",
            "Removal of Impacted Tooth, Completely Bony",
            DentalProcedureCategory.OralSurgery,
            new List<int> { 17 },
            new List<string>(),
            "PROV-001", "Dr. Smith",
            null, null, null, null, 400.00m, null);

        await wf.ReferDentalTreatmentAsync(treatmentId, "Referred to oral surgery service", "USR-006");

        List<DentalTreatmentIndexEntry> treatments = await wf.GetDentalTreatmentsAsync();
        Assert.That(treatments[0].Status, Is.EqualTo(DentalTreatmentStatus.Referred));

        DentalTreatmentState state = await wf.GetDentalTreatmentAsync(treatmentId);
        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Referred));
    }

    [Test]
    public async Task GetDentalTreatmentsByStatus_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string plannedId = await wf.RecordDentalTreatmentAsync(
            DateTime.UtcNow, "D1110", "Prophylaxis – Adult",
            DentalProcedureCategory.Preventive,
            new List<int>(), new List<string>(),
            "PROV-001", "Dr. A", null, null, null, null, 75m, null);

        string completedId = await wf.RecordDentalTreatmentAsync(
            DateTime.UtcNow, "D2140", "Amalgam, 1 Surface",
            DentalProcedureCategory.Restorative,
            new List<int> { 3 }, new List<string> { "O" },
            "PROV-001", "Dr. A", null, null, null, "Local", 85m, null);
        await wf.CompleteDentalTreatmentAsync(completedId, DateTime.UtcNow, "USR-001", null);

        List<DentalTreatmentIndexEntry> planned =
            await wf.GetDentalTreatmentsByStatusAsync(DentalTreatmentStatus.Planned);
        List<DentalTreatmentIndexEntry> completed =
            await wf.GetDentalTreatmentsByStatusAsync(DentalTreatmentStatus.Completed);

        Assert.That(planned, Has.Count.EqualTo(1));
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(planned[0].TreatmentId, Is.EqualTo(plannedId));
        Assert.That(completed[0].TreatmentId, Is.EqualTo(completedId));
    }

    [Test]
    public async Task MultipleTreatments_AreIndexedNewestFirst()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.RecordDentalTreatmentAsync(
            new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            "D0120", "Periodic Oral Evaluation",
            DentalProcedureCategory.Diagnostic,
            new List<int>(), new List<string>(),
            "PROV-001", "Dr. A", null, null, null, null, 55m, null);

        await wf.RecordDentalTreatmentAsync(
            new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            "D1110", "Prophylaxis – Adult",
            DentalProcedureCategory.Preventive,
            new List<int>(), new List<string>(),
            "PROV-001", "Dr. A", null, null, null, null, 75m, null);

        List<DentalTreatmentIndexEntry> all = await wf.GetDentalTreatmentsAsync();

        Assert.That(all, Has.Count.EqualTo(2));
        // Newest first — the June prophylaxis was added last → index position 0
        Assert.That(all[0].ProcedureCode, Is.EqualTo("D1110"));
        Assert.That(all[1].ProcedureCode, Is.EqualTo("D0120"));
    }
}
