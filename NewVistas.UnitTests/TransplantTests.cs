// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ── TransplantPatientGrain Tests ───────────────────────────────────────────────

[TestFixture]
public class TransplantPatientGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ITransplantPatientGrain GetGrain(string? patientId = null)
    {
        string id = patientId ?? $"PAT-{Guid.NewGuid()}";
        return _cluster.GrainFactory.GetGrain<ITransplantPatientGrain>($"TX-PATIENT:{id}");
    }

    private static Task RegisterDefault(ITransplantPatientGrain grain, string patientId = "PAT-001") =>
        grain.RegisterPatientAsync(
            patientId, "Jane Doe",
            new DateTime(1975, 3, 10),
            TransplantOrganType.Kidney,
            TransplantPriority.Standard,
            BloodType.OPositive,
            "A2,B7,DR1", 15m,
            "End-Stage Renal Disease", "N18.6",
            65m, 168m, null,
            "TX-CTR-01", "University Transplant Center",
            "REF-001", "Dr. Referring",
            null);

    [Test]
    public async Task CanRegisterPatient()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.PatientName, Is.EqualTo("Jane Doe"));
        Assert.That(state.OrganType, Is.EqualTo(TransplantOrganType.Kidney));
        Assert.That(state.BloodType, Is.EqualTo(BloodType.OPositive));
        Assert.That(state.HlaTyping, Is.EqualTo("A2,B7,DR1"));
        Assert.That(state.PanelReactiveAntibodyPct, Is.EqualTo(15m));
        Assert.That(state.PrimaryDiagnosis, Is.EqualTo("End-Stage Renal Disease"));
        Assert.That(state.LocationId, Is.EqualTo("TX-CTR-01"));
        Assert.That(state.ReferringProviderName, Is.EqualTo("Dr. Referring"));
    }

    [Test]
    public async Task PatientIdMatchesGrainKey()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string grainKey = $"TX-PATIENT:{patientId}";
        ITransplantPatientGrain grain = _cluster.GrainFactory.GetGrain<ITransplantPatientGrain>(grainKey);
        await RegisterDefault(grain, patientId);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.PatientId, Is.EqualTo(grainKey));
    }

    [Test]
    public async Task DefaultStatusIsPendingEvaluation()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Status, Is.EqualTo(TransplantStatus.PendingEvaluation));
    }

    [Test]
    public async Task CanUpdateStatusToListed()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        await grain.UpdateStatusAsync(TransplantStatus.Listed, null);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Status, Is.EqualTo(TransplantStatus.Listed));
    }

    [Test]
    public async Task CanUpdateStatusToOnHold()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);
        await grain.UpdateStatusAsync(TransplantStatus.Listed, null);

        await grain.UpdateStatusAsync(TransplantStatus.OnHold, "Medical condition temporarily unsuitable");

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Status, Is.EqualTo(TransplantStatus.OnHold));
    }

    [Test]
    public async Task CanUpdatePriority()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        await grain.UpdatePriorityAsync(TransplantPriority.Status1A);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Priority, Is.EqualTo(TransplantPriority.Status1A));
    }

    [Test]
    public async Task CanUpdateMeldScore()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        await grain.UpdateMeldScoreAsync(28.5m);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.CalculatedMeldScore, Is.EqualTo(28.5m));
    }

    [Test]
    public async Task CanRecordTransplant()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        DateTime txDate = DateTime.UtcNow;
        await grain.RecordTransplantAsync("TX-DONOR:123", "SURG-001", "Dr. Surgeon", txDate);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.TransplantDonorId, Is.EqualTo("TX-DONOR:123"));
        Assert.That(state.TransplantSurgeonId, Is.EqualTo("SURG-001"));
        Assert.That(state.TransplantSurgeonName, Is.EqualTo("Dr. Surgeon"));
        Assert.That(state.TransplantDate, Is.EqualTo(txDate));
    }

    [Test]
    public async Task StatusBecomesTransplantedOnRecordTransplant()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);
        await grain.UpdateStatusAsync(TransplantStatus.Listed, null);

        await grain.RecordTransplantAsync("TX-DONOR:abc", "SURG-001", "Dr. Surgeon", DateTime.UtcNow);

        TransplantPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Status, Is.EqualTo(TransplantStatus.Transplanted));
    }

    [Test]
    public async Task LastModifiedUpdatesOnStatusChange()
    {
        ITransplantPatientGrain grain = GetGrain();
        await RegisterDefault(grain);

        DateTime before = (await grain.GetPatientAsync()).LastModifiedDate;
        await Task.Delay(10);

        await grain.UpdateStatusAsync(TransplantStatus.Listed, null);

        DateTime after = (await grain.GetPatientAsync()).LastModifiedDate;
        Assert.That(after, Is.GreaterThan(before));
    }
}

// ── TransplantWaitlistIndexGrain Tests ────────────────────────────────────────

[TestFixture]
public class TransplantWaitlistIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // Each test gets its own singleton key to avoid cross-test contamination
    private ITransplantWaitlistIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<ITransplantWaitlistIndexGrain>($"TX-WAITLIST-IDX-{Guid.NewGuid()}");

    private static TransplantWaitlistEntry MakeEntry(
        string patientId,
        TransplantOrganType organ = TransplantOrganType.Kidney,
        TransplantStatus status = TransplantStatus.Listed,
        TransplantPriority priority = TransplantPriority.Standard,
        DateTime? listedDate = null) => new()
        {
            PatientId = patientId,
            PatientName = $"Patient {patientId}",
            OrganType = organ,
            Status = status,
            Priority = priority,
            ListedDate = listedDate ?? DateTime.UtcNow,
            BloodType = BloodType.OPositive,
            PrimaryDiagnosis = "Test Diagnosis",
            LocationId = "TX-CTR-01",
            LastModifiedDate = DateTime.UtcNow,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        List<TransplantWaitlistEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        string patientId = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeEntry(patientId));

        List<TransplantWaitlistEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task GetActiveReturnsListedOnly()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.Listed));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.PendingEvaluation));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.OnHold));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.Listed));

        List<TransplantWaitlistEntry> active = await index.GetActiveWaitlistAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(p => p.Status == TransplantStatus.Listed), Is.True);
    }

    [Test]
    public async Task GetByOrganFilters()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", organ: TransplantOrganType.Kidney));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", organ: TransplantOrganType.Liver));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", organ: TransplantOrganType.Kidney));

        List<TransplantWaitlistEntry> kidneyPatients = await index.GetPatientsByOrganAsync(TransplantOrganType.Kidney);
        Assert.That(kidneyPatients, Has.Count.EqualTo(2));
        Assert.That(kidneyPatients.All(p => p.OrganType == TransplantOrganType.Kidney), Is.True);
    }

    [Test]
    public async Task GetByStatusFilters()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.Listed));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.OnHold));
        await index.UpsertPatientAsync(MakeEntry($"PAT-{Guid.NewGuid()}", status: TransplantStatus.OnHold));

        List<TransplantWaitlistEntry> onHold = await index.GetPatientsByStatusAsync(TransplantStatus.OnHold);
        Assert.That(onHold, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        string patientId = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeEntry(patientId, status: TransplantStatus.Listed));

        TransplantWaitlistEntry updated = MakeEntry(patientId, status: TransplantStatus.Transplanted);
        await index.UpsertPatientAsync(updated);

        List<TransplantWaitlistEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(TransplantStatus.Transplanted));
    }

    [Test]
    public async Task OrderedByPriorityThenDate()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        DateTime older = DateTime.UtcNow.AddDays(-10);
        DateTime newer = DateTime.UtcNow.AddDays(-1);

        string idStandard = $"PAT-{Guid.NewGuid()}";
        string idStatus1A = $"PAT-{Guid.NewGuid()}";
        string idUrgent = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeEntry(idStandard, priority: TransplantPriority.Standard, listedDate: older));
        await index.UpsertPatientAsync(MakeEntry(idStatus1A, priority: TransplantPriority.Status1A, listedDate: newer));
        await index.UpsertPatientAsync(MakeEntry(idUrgent, priority: TransplantPriority.Urgent, listedDate: older));

        List<TransplantWaitlistEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all[0].PatientId, Is.EqualTo(idStatus1A)); // Status1A = highest
        Assert.That(all[1].PatientId, Is.EqualTo(idUrgent));
        Assert.That(all[2].PatientId, Is.EqualTo(idStandard));
    }

    [Test]
    public async Task RemoveIsIdempotent()
    {
        ITransplantWaitlistIndexGrain index = GetIndex();
        string patientId = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeEntry(patientId));

        await index.RemovePatientAsync(patientId);
        await index.RemovePatientAsync(patientId); // no-op

        List<TransplantWaitlistEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all, Is.Empty);
    }
}

// ── TransplantDonorGrain Tests ─────────────────────────────────────────────────

[TestFixture]
public class TransplantDonorGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ITransplantDonorGrain GetGrain() =>
        _cluster.GrainFactory.GetGrain<ITransplantDonorGrain>($"TX-DONOR:{Guid.NewGuid()}");

    private static Task CreateDefault(ITransplantDonorGrain grain, TransplantOrganType organ = TransplantOrganType.Kidney) =>
        grain.CreateDonorAsync(
            DonorType.DeceasedDonor, organ,
            "Donor-A", new DateTime(1985, 6, 20),
            BloodType.APositive,
            75m, 175m,
            "Traumatic brain injury",
            DateTime.UtcNow.AddHours(-5),
            DateTime.UtcNow.AddHours(-4),
            DateTime.UtcNow.AddHours(20),
            "A2,B44,DR4", 4m,
            "HOSP-01", "City Hospital",
            "SURG-001", "Dr. Procurement",
            null);

    [Test]
    public async Task CanCreateDonor()
    {
        ITransplantDonorGrain grain = GetGrain();
        await CreateDefault(grain);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.DonorType, Is.EqualTo(DonorType.DeceasedDonor));
        Assert.That(state.OrganType, Is.EqualTo(TransplantOrganType.Kidney));
        Assert.That(state.DonorName, Is.EqualTo("Donor-A"));
        Assert.That(state.BloodType, Is.EqualTo(BloodType.APositive));
        Assert.That(state.CauseOfDeath, Is.EqualTo("Traumatic brain injury"));
        Assert.That(state.HlaTyping, Is.EqualTo("A2,B44,DR4"));
        Assert.That(state.RecoveredByName, Is.EqualTo("Dr. Procurement"));
    }

    [Test]
    public async Task DonorIdMatchesGrainKey()
    {
        string key = $"TX-DONOR:{Guid.NewGuid()}";
        ITransplantDonorGrain grain = _cluster.GrainFactory.GetGrain<ITransplantDonorGrain>(key);
        await CreateDefault(grain);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.DonorId, Is.EqualTo(key));
    }

    [Test]
    public async Task DefaultStatusIsAvailable()
    {
        ITransplantDonorGrain grain = GetGrain();
        await CreateDefault(grain);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Available));
    }

    [Test]
    public async Task CanAllocateToPatient()
    {
        ITransplantDonorGrain grain = GetGrain();
        await CreateDefault(grain);

        DateTime allocDt = DateTime.UtcNow;
        await grain.AllocateToPatientAsync("PAT-REC-001", "John Recipient", allocDt);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Allocated));
        Assert.That(state.AllocatedToPatientId, Is.EqualTo("PAT-REC-001"));
        Assert.That(state.AllocatedToPatientName, Is.EqualTo("John Recipient"));
        Assert.That(state.AllocationDateTime, Is.EqualTo(allocDt));
    }

    [Test]
    public async Task CanRecordTransplant()
    {
        ITransplantDonorGrain grain = GetGrain();
        await CreateDefault(grain);
        await grain.AllocateToPatientAsync("PAT-REC-001", "John Recipient", DateTime.UtcNow);

        DateTime txDt = DateTime.UtcNow.AddHours(2);
        await grain.RecordTransplantAsync(txDt);

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Transplanted));
        Assert.That(state.TransplantDateTime, Is.EqualTo(txDt));
    }

    [Test]
    public async Task CanDiscardOrgan()
    {
        ITransplantDonorGrain grain = GetGrain();
        await CreateDefault(grain);

        await grain.DiscardOrganAsync("No compatible recipient found within viability window");

        TransplantDonorState state = await grain.GetDonorAsync();
        Assert.That(state.Status, Is.EqualTo(DonorStatus.Discarded));
        Assert.That(state.DiscardReason, Is.EqualTo("No compatible recipient found within viability window"));
    }
}

// ── TransplantDonorIndexGrain Tests ───────────────────────────────────────────

[TestFixture]
public class TransplantDonorIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ITransplantDonorIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<ITransplantDonorIndexGrain>($"TX-DONOR-IDX-{Guid.NewGuid()}");

    private static TransplantDonorSummaryEntry MakeEntry(
        string donorId,
        TransplantOrganType organ = TransplantOrganType.Kidney,
        DonorStatus status = DonorStatus.Available,
        DateTime? recoveryDt = null) => new()
        {
            DonorId = donorId,
            OrganType = organ,
            DonorType = DonorType.DeceasedDonor,
            BloodType = BloodType.OPositive,
            Status = status,
            DonorAgeYears = 35,
            RecoveryDateTime = recoveryDt ?? DateTime.UtcNow,
            LocationId = "HOSP-01",
        };

    [Test]
    public async Task EmptyOnStart()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        List<TransplantDonorSummaryEntry> all = await index.GetAllDonorsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        string donorId = $"TX-DONOR:{Guid.NewGuid()}";
        await index.UpsertDonorAsync(MakeEntry(donorId));

        List<TransplantDonorSummaryEntry> all = await index.GetAllDonorsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DonorId, Is.EqualTo(donorId));
    }

    [Test]
    public async Task GetAvailableFilters()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Available));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Allocated));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Available));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Discarded));

        List<TransplantDonorSummaryEntry> available = await index.GetAvailableDonorsAsync();
        Assert.That(available, Has.Count.EqualTo(2));
        Assert.That(available.All(d => d.Status == DonorStatus.Available), Is.True);
    }

    [Test]
    public async Task GetByOrganFilters()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", organ: TransplantOrganType.Kidney));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", organ: TransplantOrganType.Liver));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", organ: TransplantOrganType.Kidney));

        List<TransplantDonorSummaryEntry> kidneys = await index.GetDonorsByOrganAsync(TransplantOrganType.Kidney);
        Assert.That(kidneys, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetByStatusFilters()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Allocated));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Transplanted));
        await index.UpsertDonorAsync(MakeEntry($"TX-DONOR:{Guid.NewGuid()}", status: DonorStatus.Transplanted));

        List<TransplantDonorSummaryEntry> transplanted = await index.GetDonorsByStatusAsync(DonorStatus.Transplanted);
        Assert.That(transplanted, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        string donorId = $"TX-DONOR:{Guid.NewGuid()}";
        await index.UpsertDonorAsync(MakeEntry(donorId, status: DonorStatus.Available));

        TransplantDonorSummaryEntry updated = MakeEntry(donorId, status: DonorStatus.Allocated);
        updated.MatchedPatientId = "PAT-REC-001";
        updated.MatchedPatientName = "John Recipient";
        await index.UpsertDonorAsync(updated);

        List<TransplantDonorSummaryEntry> all = await index.GetAllDonorsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(DonorStatus.Allocated));
        Assert.That(all[0].MatchedPatientName, Is.EqualTo("John Recipient"));
    }

    [Test]
    public async Task RemoveIsIdempotent()
    {
        ITransplantDonorIndexGrain index = GetIndex();
        string donorId = $"TX-DONOR:{Guid.NewGuid()}";
        await index.UpsertDonorAsync(MakeEntry(donorId));

        await index.RemoveDonorAsync(donorId);
        await index.RemoveDonorAsync(donorId); // no-op

        List<TransplantDonorSummaryEntry> all = await index.GetAllDonorsAsync();
        Assert.That(all, Is.Empty);
    }
}
