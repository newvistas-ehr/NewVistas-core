// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Transplant module.
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end transplant patient registration, donor management,
/// organ allocation, and transplant recording workflows.
/// </summary>
[TestFixture]
public class TransplantWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ITransplantPatientGrain GetPatientGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<ITransplantPatientGrain>($"TX-PATIENT:{patientId}");

    private ITransplantWaitlistIndexGrain GetWaitlistIndex() =>
        _cluster.GrainFactory.GetGrain<ITransplantWaitlistIndexGrain>("TX-WAITLIST-IDX");

    private ITransplantDonorGrain GetDonorGrain(string donorId) =>
        _cluster.GrainFactory.GetGrain<ITransplantDonorGrain>($"TX-DONOR:{donorId}");

    private ITransplantDonorIndexGrain GetDonorIndex() =>
        _cluster.GrainFactory.GetGrain<ITransplantDonorIndexGrain>("TX-DONOR-IDX");

    private static async Task RegisterDefaultPatient(ITransplantPatientGrain grain, string patientId)
    {
        await grain.RegisterPatientAsync(
            patientId, "Transplant Patient", new DateTime(1960, 3, 15),
            TransplantOrganType.Liver, TransplantPriority.Standard,
            BloodType.APositive, "A2,A24,B7,B35,DR1,DR4", 15.0m,
            "End-Stage Liver Disease", "K74.60",
            85.0m, 175.0m, 22.0m,
            "LOC-001", "VA Transplant Center",
            "PRV-001", "Dr. Hepatologist", null);
    }

    private static async Task CreateDefaultDonor(ITransplantDonorGrain grain)
    {
        await grain.CreateDonorAsync(
            DonorType.DeceasedDonor, TransplantOrganType.Liver,
            "Donor-Anon-001", new DateTime(1975, 7, 20),
            BloodType.APositive, 80.0m, 170.0m,
            "Motor vehicle accident",
            DateTime.UtcNow.AddHours(-6),
            DateTime.UtcNow.AddHours(-4),
            DateTime.UtcNow.AddHours(18),
            "A2,A11,B7,B44,DR1,DR7", 4.0m,
            "LOC-002", "Regional OPO",
            "SURG-001", "Dr. Recovery",
            "Organ in good condition");
    }

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TransplantPatient_Register_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITransplantPatientGrain grain = GetPatientGrain(patientId);

        await RegisterDefaultPatient(grain, patientId);

        TransplantPatientState state = await grain.GetPatientAsync();
        // The grain stores full key (TX-PATIENT:{patientId}) as PatientId
        Assert.That(state.PatientId, Is.EqualTo($"TX-PATIENT:{patientId}"));
        Assert.That(state.PatientName, Is.EqualTo("Transplant Patient"));
        Assert.That(state.OrganType, Is.EqualTo(TransplantOrganType.Liver));
        Assert.That(state.Priority, Is.EqualTo(TransplantPriority.Standard));
        Assert.That(state.BloodType, Is.EqualTo(BloodType.APositive));
        Assert.That(state.PrimaryDiagnosis, Is.EqualTo("End-Stage Liver Disease"));
        Assert.That(state.CalculatedMeldScore, Is.EqualTo(22.0m));
        Assert.That(state.LocationName, Is.EqualTo("VA Transplant Center"));
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TransplantPatient_UpdateStatus_ToListed()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITransplantPatientGrain grain = GetPatientGrain(patientId);
        await RegisterDefaultPatient(grain, patientId);

        await grain.UpdateStatusAsync(TransplantStatus.Listed, null);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Status, Is.EqualTo(TransplantStatus.Listed));
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TransplantPatient_UpdatePriority_ToUrgent()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITransplantPatientGrain grain = GetPatientGrain(patientId);
        await RegisterDefaultPatient(grain, patientId);

        await grain.UpdatePriorityAsync(TransplantPriority.Urgent);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Priority, Is.EqualTo(TransplantPriority.Urgent));
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TransplantPatient_UpdateMeldScore()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITransplantPatientGrain grain = GetPatientGrain(patientId);
        await RegisterDefaultPatient(grain, patientId);

        await grain.UpdateMeldScoreAsync(35.0m);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.CalculatedMeldScore, Is.EqualTo(35.0m));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task WaitlistIndex_UpsertAndGetAll()
    {
        ITransplantWaitlistIndexGrain index = GetWaitlistIndex();

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await index.UpsertPatientAsync(new TransplantWaitlistEntry
        {
            PatientId = patientId,
            PatientName = "Waitlist Patient",
            OrganType = TransplantOrganType.Kidney,
            Status = TransplantStatus.Listed,
            Priority = TransplantPriority.Standard,
            ListedDate = DateTime.UtcNow,
            BloodType = BloodType.OPositive,
            AgeYears = 55,
            PrimaryDiagnosis = "ESRD",
            LocationId = "LOC-001",
            LastModifiedDate = DateTime.UtcNow
        });

        List<TransplantWaitlistEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all.Any(p => p.PatientId == patientId), Is.True);
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task WaitlistIndex_GetByOrgan_FiltersCorrectly()
    {
        ITransplantWaitlistIndexGrain index = GetWaitlistIndex();

        string kidneyId = $"PAT-{Guid.NewGuid():N}";
        string liverId = $"PAT-{Guid.NewGuid():N}";

        await index.UpsertPatientAsync(new TransplantWaitlistEntry
        {
            PatientId = kidneyId, PatientName = "Kidney Patient",
            OrganType = TransplantOrganType.Kidney, Status = TransplantStatus.Listed,
            Priority = TransplantPriority.Standard, ListedDate = DateTime.UtcNow,
            BloodType = BloodType.OPositive, AgeYears = 50,
            PrimaryDiagnosis = "ESRD", LocationId = "LOC-001", LastModifiedDate = DateTime.UtcNow
        });
        await index.UpsertPatientAsync(new TransplantWaitlistEntry
        {
            PatientId = liverId, PatientName = "Liver Patient",
            OrganType = TransplantOrganType.Liver, Status = TransplantStatus.Listed,
            Priority = TransplantPriority.Urgent, ListedDate = DateTime.UtcNow,
            BloodType = BloodType.APositive, AgeYears = 60,
            PrimaryDiagnosis = "ESLD", LocationId = "LOC-001", LastModifiedDate = DateTime.UtcNow
        });

        List<TransplantWaitlistEntry> kidneys = await index.GetPatientsByOrganAsync(TransplantOrganType.Kidney);
        Assert.That(kidneys.Any(p => p.PatientId == kidneyId), Is.True);
        Assert.That(kidneys.All(p => p.OrganType == TransplantOrganType.Kidney), Is.True);
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task WaitlistIndex_GetActiveWaitlist_ReturnsOnlyListed()
    {
        ITransplantWaitlistIndexGrain index = GetWaitlistIndex();

        string listedId = $"PAT-{Guid.NewGuid():N}";
        string removedId = $"PAT-{Guid.NewGuid():N}";

        await index.UpsertPatientAsync(new TransplantWaitlistEntry
        {
            PatientId = listedId, PatientName = "Listed Patient",
            OrganType = TransplantOrganType.Heart, Status = TransplantStatus.Listed,
            Priority = TransplantPriority.Status1A, ListedDate = DateTime.UtcNow,
            BloodType = BloodType.ONegative, AgeYears = 45,
            PrimaryDiagnosis = "Cardiomyopathy", LocationId = "LOC-001", LastModifiedDate = DateTime.UtcNow
        });
        await index.UpsertPatientAsync(new TransplantWaitlistEntry
        {
            PatientId = removedId, PatientName = "Removed Patient",
            OrganType = TransplantOrganType.Heart, Status = TransplantStatus.Removed,
            Priority = TransplantPriority.Standard, ListedDate = DateTime.UtcNow.AddDays(-30),
            BloodType = BloodType.ABPositive, AgeYears = 65,
            PrimaryDiagnosis = "CHF", LocationId = "LOC-001", LastModifiedDate = DateTime.UtcNow
        });

        List<TransplantWaitlistEntry> active = await index.GetActiveWaitlistAsync();
        Assert.That(active.Any(p => p.PatientId == listedId), Is.True);
        Assert.That(active.All(p => p.Status == TransplantStatus.Listed), Is.True);
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Donor_Create_PersistsAllFields()
    {
        string donorId = Guid.NewGuid().ToString("N");
        ITransplantDonorGrain grain = GetDonorGrain(donorId);

        await CreateDefaultDonor(grain);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.DonorType, Is.EqualTo(DonorType.DeceasedDonor));
        Assert.That(state.OrganType, Is.EqualTo(TransplantOrganType.Liver));
        Assert.That(state.BloodType, Is.EqualTo(BloodType.APositive));
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Available));
        Assert.That(state.CauseOfDeath, Is.EqualTo("Motor vehicle accident"));
        Assert.That(state.ColdIschemiaTimeHours, Is.EqualTo(4.0m));
        Assert.That(state.RecoveredByName, Is.EqualTo("Dr. Recovery"));
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Donor_AllocateToPatient_SetsAllocationFields()
    {
        string donorId = Guid.NewGuid().ToString("N");
        ITransplantDonorGrain grain = GetDonorGrain(donorId);
        await CreateDefaultDonor(grain);

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await grain.AllocateToPatientAsync(patientId, "Recipient Patient", DateTime.UtcNow);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Allocated));
        Assert.That(state.AllocatedToPatientId, Is.EqualTo(patientId));
        Assert.That(state.AllocatedToPatientName, Is.EqualTo("Recipient Patient"));
        Assert.That(state.AllocationDateTime, Is.Not.Null);
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Donor_RecordTransplant_SetsTransplantedStatus()
    {
        string donorId = Guid.NewGuid().ToString("N");
        ITransplantDonorGrain grain = GetDonorGrain(donorId);
        await CreateDefaultDonor(grain);

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await grain.AllocateToPatientAsync(patientId, "Recipient", DateTime.UtcNow);
        await grain.RecordTransplantAsync(DateTime.UtcNow);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Transplanted));
        Assert.That(state.TransplantDateTime, Is.Not.Null);
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Donor_DiscardOrgan_SetsDiscardedStatus()
    {
        string donorId = Guid.NewGuid().ToString("N");
        ITransplantDonorGrain grain = GetDonorGrain(donorId);
        await CreateDefaultDonor(grain);

        await grain.DiscardOrganAsync("Cold ischemia time exceeded");

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Discarded));
        Assert.That(state.DiscardReason, Is.EqualTo("Cold ischemia time exceeded"));
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DonorIndex_UpsertAndGetAvailable()
    {
        ITransplantDonorIndexGrain index = GetDonorIndex();

        string availableId = Guid.NewGuid().ToString("N");
        string discardedId = Guid.NewGuid().ToString("N");

        await index.UpsertDonorAsync(new TransplantDonorSummaryEntry
        {
            DonorId = availableId, OrganType = TransplantOrganType.Kidney,
            DonorType = DonorType.DeceasedDonor, BloodType = BloodType.OPositive,
            Status = DonorStatus.Available, DonorAgeYears = 40,
            RecoveryDateTime = DateTime.UtcNow, LocationId = "LOC-001"
        });
        await index.UpsertDonorAsync(new TransplantDonorSummaryEntry
        {
            DonorId = discardedId, OrganType = TransplantOrganType.Kidney,
            DonorType = DonorType.DeceasedDonor, BloodType = BloodType.BNegative,
            Status = DonorStatus.Discarded, DonorAgeYears = 65,
            RecoveryDateTime = DateTime.UtcNow.AddDays(-1), LocationId = "LOC-002"
        });

        List<TransplantDonorSummaryEntry> available = await index.GetAvailableDonorsAsync();
        Assert.That(available.Any(d => d.DonorId == availableId), Is.True);
        Assert.That(available.All(d => d.Status == DonorStatus.Available), Is.True);
    }

    // ── 13 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DonorIndex_GetByOrgan_FiltersCorrectly()
    {
        ITransplantDonorIndexGrain index = GetDonorIndex();

        string liverDonorId = Guid.NewGuid().ToString("N");
        string heartDonorId = Guid.NewGuid().ToString("N");

        await index.UpsertDonorAsync(new TransplantDonorSummaryEntry
        {
            DonorId = liverDonorId, OrganType = TransplantOrganType.Liver,
            DonorType = DonorType.DeceasedDonor, BloodType = BloodType.APositive,
            Status = DonorStatus.Available, DonorAgeYears = 35,
            RecoveryDateTime = DateTime.UtcNow, LocationId = "LOC-001"
        });
        await index.UpsertDonorAsync(new TransplantDonorSummaryEntry
        {
            DonorId = heartDonorId, OrganType = TransplantOrganType.Heart,
            DonorType = DonorType.DeceasedDonor, BloodType = BloodType.ONegative,
            Status = DonorStatus.Available, DonorAgeYears = 28,
            RecoveryDateTime = DateTime.UtcNow, LocationId = "LOC-002"
        });

        List<TransplantDonorSummaryEntry> livers = await index.GetDonorsByOrganAsync(TransplantOrganType.Liver);
        Assert.That(livers.Any(d => d.DonorId == liverDonorId), Is.True);
        Assert.That(livers.All(d => d.OrganType == TransplantOrganType.Liver), Is.True);
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task EndToEnd_PatientRegisteredDonorAllocatedTransplantRecorded()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string donorId = Guid.NewGuid().ToString("N");

        // Step 1: Register patient
        ITransplantPatientGrain patient = GetPatientGrain(patientId);
        await patient.RegisterPatientAsync(
            patientId, "E2E Recipient", new DateTime(1960, 1, 1),
            TransplantOrganType.Liver, TransplantPriority.Urgent,
            BloodType.APositive, "A2,A24,B7,B35,DR1,DR4", 25.0m,
            "Hepatocellular carcinoma", "C22.0",
            78.0m, 172.0m, 30.0m,
            "LOC-001", "VA Transplant Center",
            "PRV-001", "Dr. Hepatologist", null);

        await patient.UpdateStatusAsync(TransplantStatus.Listed, null);

        // Step 2: Create donor
        ITransplantDonorGrain donor = GetDonorGrain(donorId);
        await donor.CreateDonorAsync(
            DonorType.DeceasedDonor, TransplantOrganType.Liver,
            "Donor-E2E", new DateTime(1980, 5, 10),
            BloodType.APositive, 82.0m, 178.0m,
            "Cerebrovascular accident",
            DateTime.UtcNow.AddHours(-8),
            DateTime.UtcNow.AddHours(-6),
            DateTime.UtcNow.AddHours(16),
            "A2,A11,B7,B44,DR1,DR7", 6.0m,
            "LOC-003", "Regional OPO",
            "SURG-002", "Dr. Procurement", null);

        // Step 3: Allocate organ to patient
        await donor.AllocateToPatientAsync(patientId, "E2E Recipient", DateTime.UtcNow);

        // Step 4: Record transplant on both sides
        DateTime transplantDate = DateTime.UtcNow;
        await donor.RecordTransplantAsync(transplantDate);
        await patient.RecordTransplantAsync(donorId, "SURG-003", "Dr. Transplant Surgeon", transplantDate);

        // Verify patient state
        TransplantPatientState patientState = await patient.GetPatientAsync();
        Assert.That(patientState.Status, Is.EqualTo(TransplantStatus.Transplanted));
        Assert.That(patientState.TransplantDonorId, Is.EqualTo(donorId));
        Assert.That(patientState.TransplantSurgeonName, Is.EqualTo("Dr. Transplant Surgeon"));
        Assert.That(patientState.TransplantDate, Is.Not.Null);

        // Verify donor state
        TransplantDonorState donorState = await donor.GetDonorAsync();
        Assert.That(donorState.Status, Is.EqualTo(DonorStatus.Transplanted));
        Assert.That(donorState.AllocatedToPatientId, Is.EqualTo(patientId));
        Assert.That(donorState.TransplantDateTime, Is.Not.Null);
    }
}
