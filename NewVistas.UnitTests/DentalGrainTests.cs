// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for VistA Dental — Files #228 (DENTAL PATIENT) and #228.1 (DENTAL TREATMENT).
/// Tests individual grains directly via TestCluster (not via the workflow grain).
/// </summary>
[TestFixture]
public class DentalGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── IDentalPatientGrain ──────────────────────────────────────────────────

    [Test]
    public async Task DentalPatientGrain_EnsureInitialized_SetsPatientId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalPatientGrain grain = _cluster.GrainFactory.GetGrain<IDentalPatientGrain>(
            $"DENTAL-PATIENT:{patientId}");

        await grain.EnsureInitializedAsync(patientId);
        DentalPatientState state = await grain.GetAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.EligibilityStatus, Is.EqualTo(DentalEligibilityStatus.Unknown));
        Assert.That(state.PeriodontalStatus, Is.EqualTo(DentalPeriodontalStatus.Healthy));
    }

    [Test]
    public async Task DentalPatientGrain_EnsureInitialized_IsIdempotent()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalPatientGrain grain = _cluster.GrainFactory.GetGrain<IDentalPatientGrain>(
            $"DENTAL-PATIENT:{patientId}");

        await grain.EnsureInitializedAsync(patientId);
        await grain.EnsureInitializedAsync(patientId); // second call must not throw or overwrite

        DentalPatientState state = await grain.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task DentalPatientGrain_UpdateEligibility_PersistsCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalPatientGrain grain = _cluster.GrainFactory.GetGrain<IDentalPatientGrain>(
            $"DENTAL-PATIENT:{patientId}");
        await grain.EnsureInitializedAsync(patientId);

        await grain.UpdateEligibilityAsync(
            DentalEligibilityStatus.Eligible,
            "SC",
            "Service-Connected Disability");

        DentalPatientState state = await grain.GetAsync();

        Assert.That(state.EligibilityStatus, Is.EqualTo(DentalEligibilityStatus.Eligible));
        Assert.That(state.EligibilityBasisCode, Is.EqualTo("SC"));
        Assert.That(state.EligibilityBasisDescription, Is.EqualTo("Service-Connected Disability"));
    }

    [Test]
    public async Task DentalPatientGrain_SetPrimaryDentist_PersistsCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalPatientGrain grain = _cluster.GrainFactory.GetGrain<IDentalPatientGrain>(
            $"DENTAL-PATIENT:{patientId}");
        await grain.EnsureInitializedAsync(patientId);

        await grain.SetPrimaryDentistAsync("DENT-001", "Dr. Jane Smith");

        DentalPatientState state = await grain.GetAsync();

        Assert.That(state.PrimaryDentistId, Is.EqualTo("DENT-001"));
        Assert.That(state.PrimaryDentistName, Is.EqualTo("Dr. Jane Smith"));
    }

    [Test]
    public async Task DentalPatientGrain_UpdateClinicalStatus_PersistsCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalPatientGrain grain = _cluster.GrainFactory.GetGrain<IDentalPatientGrain>(
            $"DENTAL-PATIENT:{patientId}");
        await grain.EnsureInitializedAsync(patientId);

        await grain.UpdateClinicalStatusAsync(
            DentalPeriodontalStatus.GingivitisLocalized,
            "Full upper denture",
            16,
            true,
            "Patient tolerates treatment well.");

        DentalPatientState state = await grain.GetAsync();

        Assert.That(state.PeriodontalStatus, Is.EqualTo(DentalPeriodontalStatus.GingivitisLocalized));
        Assert.That(state.ProstheticStatus, Is.EqualTo("Full upper denture"));
        Assert.That(state.RemainingTeethCount, Is.EqualTo(16));
        Assert.That(state.OnFluoride, Is.True);
        Assert.That(state.ClinicalNotes, Is.EqualTo("Patient tolerates treatment well."));
    }

    [Test]
    public async Task DentalPatientGrain_RecordVisitDates_UpdatesOnlyNonNullDates()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalPatientGrain grain = _cluster.GrainFactory.GetGrain<IDentalPatientGrain>(
            $"DENTAL-PATIENT:{patientId}");
        await grain.EnsureInitializedAsync(patientId);

        DateTime examDate    = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        DateTime cleanDate   = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        await grain.RecordVisitDatesAsync(examDate, null, cleanDate);

        DentalPatientState state = await grain.GetAsync();

        Assert.That(state.LastExamDate, Is.EqualTo(examDate));
        Assert.That(state.LastXRayDate, Is.Null);
        Assert.That(state.LastCleaningDate, Is.EqualTo(cleanDate));
    }

    // ─── IDentalTreatmentGrain ────────────────────────────────────────────────

    [Test]
    public async Task DentalTreatmentGrain_Create_SetsInitialState()
    {
        string treatmentId = $"DENTAL-TX:{Guid.NewGuid()}";
        IDentalTreatmentGrain grain = _cluster.GrainFactory.GetGrain<IDentalTreatmentGrain>(treatmentId);

        DateTime txDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(
            "PATIENT-001",
            txDate,
            "D2140",
            "Amalgam Restoration, One Surface, Permanent Tooth",
            DentalProcedureCategory.Restorative,
            new List<int> { 14 },
            new List<string> { "O" },
            "PROV-001",
            "Dr. John Doe",
            "LOC-001",
            "Dental Clinic A",
            "K02.51",
            "Local",
            85.00m,
            "Patient tolerated procedure well.");

        DentalTreatmentState state = await grain.GetAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.ProcedureCode, Is.EqualTo("D2140"));
        Assert.That(state.ProcedureCategory, Is.EqualTo(DentalProcedureCategory.Restorative));
        Assert.That(state.ToothNumbers, Contains.Item(14));
        Assert.That(state.Surfaces, Contains.Item("O"));
        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Planned));
        Assert.That(state.ChargeAmount, Is.EqualTo(85.00m));
        Assert.That(state.TreatmentDate, Is.EqualTo(txDate));
    }

    [Test]
    public async Task DentalTreatmentGrain_Complete_SetsCompletedStatus()
    {
        string treatmentId = $"DENTAL-TX:{Guid.NewGuid()}";
        IDentalTreatmentGrain grain = _cluster.GrainFactory.GetGrain<IDentalTreatmentGrain>(treatmentId);

        await grain.CreateAsync(
            "PATIENT-002",
            DateTime.UtcNow,
            "D1110",
            "Prophylaxis – Adult",
            DentalProcedureCategory.Preventive,
            new List<int>(),
            new List<string>(),
            "PROV-001", "Dr. Jane Smith",
            null, null, null, null, 75.00m, null);

        DateTime completedDate = DateTime.UtcNow;
        await grain.CompleteAsync(completedDate, "USR-001", "Completed without complications.");

        DentalTreatmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Completed));
        Assert.That(state.CompletedDate, Is.EqualTo(completedDate).Within(TimeSpan.FromSeconds(1)));
        Assert.That(state.Notes, Is.EqualTo("Completed without complications."));
    }

    [Test]
    public async Task DentalTreatmentGrain_Cancel_SetsCancelledStatus()
    {
        string treatmentId = $"DENTAL-TX:{Guid.NewGuid()}";
        IDentalTreatmentGrain grain = _cluster.GrainFactory.GetGrain<IDentalTreatmentGrain>(treatmentId);

        await grain.CreateAsync(
            "PATIENT-003",
            DateTime.UtcNow,
            "D3310",
            "Endodontic Therapy, Anterior Tooth",
            DentalProcedureCategory.Endodontic,
            new List<int> { 9 },
            new List<string>(),
            "PROV-001", "Dr. Smith",
            null, null, "K04.0", null, 650.00m, null);

        await grain.CancelAsync("Patient refused treatment", "USR-002");

        DentalTreatmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Cancelled));
        Assert.That(state.StatusReason, Is.EqualTo("Patient refused treatment"));
    }

    [Test]
    public async Task DentalTreatmentGrain_Refer_SetsReferredStatus()
    {
        string treatmentId = $"DENTAL-TX:{Guid.NewGuid()}";
        IDentalTreatmentGrain grain = _cluster.GrainFactory.GetGrain<IDentalTreatmentGrain>(treatmentId);

        await grain.CreateAsync(
            "PATIENT-004",
            DateTime.UtcNow,
            "D7110",
            "Extraction, Erupted Tooth",
            DentalProcedureCategory.OralSurgery,
            new List<int> { 17 },
            new List<string>(),
            "PROV-001", "Dr. Smith",
            null, null, null, null, 200.00m, null);

        await grain.ReferAsync("Referred to oral surgeon — impacted wisdom tooth", "USR-003");

        DentalTreatmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DentalTreatmentStatus.Referred));
        Assert.That(state.StatusReason, Does.Contain("oral surgeon"));
    }

    // ─── IDentalTreatmentIndexGrain ───────────────────────────────────────────

    [Test]
    public async Task DentalTreatmentIndexGrain_AddEntry_AppearsInGetAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalTreatmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDentalTreatmentIndexGrain>(
            $"DENTAL-TX-IDX:{patientId}");

        DentalTreatmentIndexEntry entry = new()
        {
            TreatmentId          = $"DENTAL-TX:{Guid.NewGuid()}",
            PatientId            = patientId,
            ProcedureCode        = "D2140",
            ProcedureDescription = "Amalgam, 1 Surface",
            ProcedureCategory    = DentalProcedureCategory.Restorative,
            ToothNumbers         = "14",
            TreatmentDate        = DateTime.UtcNow,
            ProviderName         = "Dr. Smith",
            Status               = DentalTreatmentStatus.Planned,
            ChargeAmount         = 85.00m,
        };

        await index.AddEntryAsync(entry);
        List<DentalTreatmentIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ProcedureCode, Is.EqualTo("D2140"));
    }

    [Test]
    public async Task DentalTreatmentIndexGrain_UpdateEntryStatus_ChangesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalTreatmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDentalTreatmentIndexGrain>(
            $"DENTAL-TX-IDX:{patientId}");

        string txId = $"DENTAL-TX:{Guid.NewGuid()}";
        await index.AddEntryAsync(new DentalTreatmentIndexEntry
        {
            TreatmentId          = txId,
            PatientId            = patientId,
            ProcedureCode        = "D1110",
            ProcedureDescription = "Prophylaxis",
            ProcedureCategory    = DentalProcedureCategory.Preventive,
            TreatmentDate        = DateTime.UtcNow,
            ProviderName         = "Dr. Jones",
            Status               = DentalTreatmentStatus.Planned,
        });

        await index.UpdateEntryStatusAsync(txId, DentalTreatmentStatus.Completed);

        List<DentalTreatmentIndexEntry> all = await index.GetAllAsync();
        Assert.That(all[0].Status, Is.EqualTo(DentalTreatmentStatus.Completed));
    }

    [Test]
    public async Task DentalTreatmentIndexGrain_GetByStatus_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IDentalTreatmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDentalTreatmentIndexGrain>(
            $"DENTAL-TX-IDX:{patientId}");

        await index.AddEntryAsync(new DentalTreatmentIndexEntry
        {
            TreatmentId = $"DENTAL-TX:{Guid.NewGuid()}", PatientId = patientId,
            ProcedureCode = "D2140", ProcedureDescription = "Amalgam",
            ProcedureCategory = DentalProcedureCategory.Restorative,
            TreatmentDate = DateTime.UtcNow, ProviderName = "Dr. A",
            Status = DentalTreatmentStatus.Completed,
        });
        await index.AddEntryAsync(new DentalTreatmentIndexEntry
        {
            TreatmentId = $"DENTAL-TX:{Guid.NewGuid()}", PatientId = patientId,
            ProcedureCode = "D3310", ProcedureDescription = "Root Canal",
            ProcedureCategory = DentalProcedureCategory.Endodontic,
            TreatmentDate = DateTime.UtcNow, ProviderName = "Dr. B",
            Status = DentalTreatmentStatus.Planned,
        });

        List<DentalTreatmentIndexEntry> planned = await index.GetByStatusAsync(DentalTreatmentStatus.Planned);
        List<DentalTreatmentIndexEntry> completed = await index.GetByStatusAsync(DentalTreatmentStatus.Completed);

        Assert.That(planned, Has.Count.EqualTo(1));
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(planned[0].ProcedureCode, Is.EqualTo("D3310"));
        Assert.That(completed[0].ProcedureCode, Is.EqualTo("D2140"));
    }
}
