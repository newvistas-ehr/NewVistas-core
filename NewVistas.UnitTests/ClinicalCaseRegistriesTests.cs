// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ── ClinicalRegistryEntryGrain Tests ─────────────────────────────────────────

[TestFixture]
public class ClinicalRegistryEntryGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task<IClinicalRegistryEntryGrain> EnrolledGrain(string key, RegistryType type = RegistryType.HIV)
    {
        IClinicalRegistryEntryGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryEntryGrain>(key);
        await grain.EnrollPatientAsync(
            "PAT-001", "John Veteran", new DateTime(1975, 4, 12), type,
            "PROV-1", "Dr. Infect", "VAMC-01", "VA Medical Center 1",
            "PROV-1", "Dr. Infect", "Initial enrollment");
        return grain;
    }

    [Test]
    public async Task ClinicalRegistryEntry_CanEnrollPatient()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.PatientName, Is.EqualTo("John Veteran"));
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.HIV));
    }

    [Test]
    public async Task ClinicalRegistryEntry_EntryIdMatchesGrainKey()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.EntryId, Is.EqualTo(key));
    }

    [Test]
    public async Task ClinicalRegistryEntry_DefaultStatusIsActive()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(CCREnrollmentStatus.Active));
    }

    [Test]
    public async Task ClinicalRegistryEntry_HIVFieldsNullBeforeUpdate()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key, RegistryType.HIV);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.HIVStage, Is.Null);
        Assert.That(state.CD4CountCellsPerMm3, Is.Null);
        Assert.That(state.ViralLoadCopiesPerMl, Is.Null);
    }

    [Test]
    public async Task ClinicalRegistryEntry_CanUpdateHIVData()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        await grain.UpdateHIVDataAsync(
            HIVStage.Stage1, 450m, DateTime.UtcNow,
            200m, DateTime.UtcNow, true,
            new DateTime(2020, 1, 15), "Biktarvy");
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.HIVStage, Is.EqualTo(HIVStage.Stage1));
        Assert.That(state.CD4CountCellsPerMm3, Is.EqualTo(450m));
        Assert.That(state.CurrentARTRegimen, Is.EqualTo("Biktarvy"));
    }

    [Test]
    public async Task ClinicalRegistryEntry_IsVirallySuppressedSetCorrectly()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        await grain.UpdateHIVDataAsync(
            HIVStage.Stage1, 600m, DateTime.UtcNow,
            48m, DateTime.UtcNow, true,
            null, null);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.IsVirallySuppressed, Is.True);
    }

    [Test]
    public async Task ClinicalRegistryEntry_CanUpdateHepCData()
    {
        string key = $"CCR:HepatitisCVirus:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key, RegistryType.HepatitisCVirus);
        await grain.UpdateHepCDataAsync(
            HepCGenotype.Genotype1a, 7.5m,
            HepCTreatmentStatus.SVRAchieved,
            new DateTime(2021, 3, 1), new DateTime(2021, 11, 1),
            true, new DateTime(2022, 1, 15));
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.HepCGenotype, Is.EqualTo(HepCGenotype.Genotype1a));
        Assert.That(state.SVRAchieved, Is.True);
        Assert.That(state.FibrosisScoreKpa, Is.EqualTo(7.5m));
    }

    [Test]
    public async Task ClinicalRegistryEntry_CanUpdateDiabetesData()
    {
        string key = $"CCR:DiabetesMellitus:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key, RegistryType.DiabetesMellitus);
        var complications = new List<string> { "Retinopathy", "Nephropathy" };
        await grain.UpdateDiabetesDataAsync(DiabetesType.Type2, 8.1m, DateTime.UtcNow, false, complications);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesType, Is.EqualTo(DiabetesType.Type2));
        Assert.That(state.HbA1cPct, Is.EqualTo(8.1m));
        Assert.That(state.DiabetesComplications, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ClinicalRegistryEntry_CanUpdateEnrollmentStatus()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        await grain.UpdateEnrollmentStatusAsync(CCREnrollmentStatus.TransferredOut, DateTime.UtcNow, "Transferred to another VAMC");
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(CCREnrollmentStatus.TransferredOut));
        Assert.That(state.DeactivationReason, Is.EqualTo("Transferred to another VAMC"));
        Assert.That(state.DeactivationDate, Is.Not.Null);
    }

    [Test]
    public async Task ClinicalRegistryEntry_CreatedAndLastModifiedDateSet()
    {
        string key = $"CCR:HIV:{Guid.NewGuid()}";
        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        IClinicalRegistryEntryGrain grain = await EnrolledGrain(key);
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.CreatedDate, Is.GreaterThan(before));
        Assert.That(state.LastModifiedDate, Is.GreaterThan(before));
    }
}

// ── PatientRegistryListGrain Tests ───────────────────────────────────────────

[TestFixture]
public class PatientRegistryListGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static PatientRegistryEnrollmentEntry MakeEnrollment(
        RegistryType type,
        CCREnrollmentStatus status = CCREnrollmentStatus.Active) => new()
    {
        RegistryType = type,
        Status = status,
        EnrollmentDate = DateTime.UtcNow,
        LastModifiedDate = DateTime.UtcNow,
        PrimaryProviderName = "Dr. Test"
    };

    [Test]
    public async Task PatientRegistryList_EmptyOnStart()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        List<PatientRegistryEnrollmentEntry> all = await grain.GetAllEnrollmentsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task PatientRegistryList_CanUpsertAndRetrieve()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HIV));
        List<PatientRegistryEnrollmentEntry> all = await grain.GetAllEnrollmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].RegistryType, Is.EqualTo(RegistryType.HIV));
    }

    [Test]
    public async Task PatientRegistryList_GetActiveEnrollmentsFiltersInactive()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HIV, CCREnrollmentStatus.Active));
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HepatitisCVirus, CCREnrollmentStatus.Inactive));
        List<PatientRegistryEnrollmentEntry> active = await grain.GetActiveEnrollmentsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].RegistryType, Is.EqualTo(RegistryType.HIV));
    }

    [Test]
    public async Task PatientRegistryList_UpsertUpdatesExisting()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HIV, CCREnrollmentStatus.Active));
        PatientRegistryEnrollmentEntry updated = MakeEnrollment(RegistryType.HIV, CCREnrollmentStatus.TransferredOut);
        await grain.UpsertEnrollmentAsync(updated);
        List<PatientRegistryEnrollmentEntry> all = await grain.GetAllEnrollmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(CCREnrollmentStatus.TransferredOut));
    }

    [Test]
    public async Task PatientRegistryList_RemoveByRegistryType()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HIV));
        await grain.RemoveEnrollmentAsync(RegistryType.HIV);
        List<PatientRegistryEnrollmentEntry> all = await grain.GetAllEnrollmentsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task PatientRegistryList_MultipleRegistryTypesTracked()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HIV));
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HepatitisCVirus));
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.DiabetesMellitus));
        List<PatientRegistryEnrollmentEntry> all = await grain.GetAllEnrollmentsAsync();
        Assert.That(all, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task PatientRegistryList_RemoveOneTypeKeepsOthers()
    {
        string key = $"CCR-PAT:{Guid.NewGuid()}";
        IPatientRegistryListGrain grain = _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>(key);
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.HIV));
        await grain.UpsertEnrollmentAsync(MakeEnrollment(RegistryType.DiabetesMellitus));
        await grain.RemoveEnrollmentAsync(RegistryType.HIV);
        List<PatientRegistryEnrollmentEntry> all = await grain.GetAllEnrollmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].RegistryType, Is.EqualTo(RegistryType.DiabetesMellitus));
    }
}

// ── ClinicalRegistryIndexGrain Tests ─────────────────────────────────────────

[TestFixture]
public class ClinicalRegistryIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static CCREntrySummary MakeEntry(
        string patientId,
        RegistryType type = RegistryType.HIV,
        CCREnrollmentStatus status = CCREnrollmentStatus.Active,
        DateTime? enrollDate = null) => new()
    {
        PatientId = patientId,
        PatientName = "Registry Patient",
        RegistryType = type,
        Status = status,
        EnrollmentDate = enrollDate ?? DateTime.UtcNow,
        SiteId = "VAMC-01",
        PrimaryProviderName = "Dr. Test",
        LastModifiedDate = DateTime.UtcNow
    };

    [Test]
    public async Task ClinicalRegistryIndex_EmptyOnStart()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task ClinicalRegistryIndex_CanUpsertAndRetrieve()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-A"));
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientId, Is.EqualTo("PAT-A"));
    }

    [Test]
    public async Task ClinicalRegistryIndex_GetActiveEntriesFilters()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-ACTIVE", status: CCREnrollmentStatus.Active));
        await grain.UpsertEntryAsync(MakeEntry("PAT-INACTIVE", status: CCREnrollmentStatus.Inactive));
        List<CCREntrySummary> active = await grain.GetActiveEntriesAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].PatientId, Is.EqualTo("PAT-ACTIVE"));
    }

    [Test]
    public async Task ClinicalRegistryIndex_GetByStatusFilters()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-LTFU1", status: CCREnrollmentStatus.LostToFollowUp));
        await grain.UpsertEntryAsync(MakeEntry("PAT-LTFU2", status: CCREnrollmentStatus.LostToFollowUp));
        await grain.UpsertEntryAsync(MakeEntry("PAT-ACT", status: CCREnrollmentStatus.Active));
        List<CCREntrySummary> ltfu = await grain.GetByStatusAsync(CCREnrollmentStatus.LostToFollowUp);
        Assert.That(ltfu, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ClinicalRegistryIndex_UpsertUpdatesExisting()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-UPD", status: CCREnrollmentStatus.Active));
        CCREntrySummary updated = MakeEntry("PAT-UPD", status: CCREnrollmentStatus.Deceased);
        await grain.UpsertEntryAsync(updated);
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(CCREnrollmentStatus.Deceased));
    }

    [Test]
    public async Task ClinicalRegistryIndex_RemoveIsIdempotent()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-DEL"));
        await grain.RemoveEntryAsync("PAT-DEL");
        await grain.RemoveEntryAsync("PAT-DEL"); // idempotent
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task ClinicalRegistryIndex_OrderedNewestFirst()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        DateTime older = DateTime.UtcNow.AddDays(-30);
        DateTime newer = DateTime.UtcNow;
        await grain.UpsertEntryAsync(MakeEntry("PAT-OLD", enrollDate: older));
        await grain.UpsertEntryAsync(MakeEntry("PAT-NEW", enrollDate: newer));
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all[0].PatientId, Is.EqualTo("PAT-NEW"));
    }

    [Test]
    public async Task ClinicalRegistryIndex_MultiplePatients()
    {
        string key = $"CCR-IDX:HIV-{Guid.NewGuid()}";
        IClinicalRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>(key);
        for (int i = 1; i <= 5; i++)
            await grain.UpsertEntryAsync(MakeEntry($"PAT-{i:D3}"));
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Has.Count.EqualTo(5));
    }
}

// ── ClinicalRegistrySiteIndexGrain Tests ─────────────────────────────────────

[TestFixture]
public class ClinicalRegistrySiteIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static CCREntrySummary MakeEntry(
        string patientId,
        RegistryType type,
        DateTime? enrollDate = null) => new()
    {
        PatientId = patientId,
        PatientName = "Site Patient",
        RegistryType = type,
        Status = CCREnrollmentStatus.Active,
        EnrollmentDate = enrollDate ?? DateTime.UtcNow,
        SiteId = "VAMC-01",
        PrimaryProviderName = "Dr. Test",
        LastModifiedDate = DateTime.UtcNow
    };

    [Test]
    public async Task ClinicalRegistrySiteIndex_EmptyOnStart()
    {
        string key = $"CCR-SITE-IDX-{Guid.NewGuid()}";
        IClinicalRegistrySiteIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(key);
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task ClinicalRegistrySiteIndex_CanUpsertCrossType()
    {
        string key = $"CCR-SITE-IDX-{Guid.NewGuid()}";
        IClinicalRegistrySiteIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-A", RegistryType.HIV));
        await grain.UpsertEntryAsync(MakeEntry("PAT-B", RegistryType.HepatitisCVirus));
        await grain.UpsertEntryAsync(MakeEntry("PAT-C", RegistryType.DiabetesMellitus));
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ClinicalRegistrySiteIndex_GetRecentLimitsCount()
    {
        string key = $"CCR-SITE-IDX-{Guid.NewGuid()}";
        IClinicalRegistrySiteIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(key);
        for (int i = 1; i <= 10; i++)
            await grain.UpsertEntryAsync(MakeEntry($"PAT-{i:D3}", RegistryType.HIV, DateTime.UtcNow.AddDays(-i)));
        List<CCREntrySummary> recent = await grain.GetRecentEnrollmentsAsync(3);
        Assert.That(recent, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ClinicalRegistrySiteIndex_UpsertUpdatesExisting()
    {
        string key = $"CCR-SITE-IDX-{Guid.NewGuid()}";
        IClinicalRegistrySiteIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-UPD", RegistryType.HIV));
        CCREntrySummary updated = MakeEntry("PAT-UPD", RegistryType.HIV);
        updated.Status = CCREnrollmentStatus.Deceased;
        await grain.UpsertEntryAsync(updated);
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(CCREnrollmentStatus.Deceased));
    }

    [Test]
    public async Task ClinicalRegistrySiteIndex_RemoveByPatientAndType()
    {
        string key = $"CCR-SITE-IDX-{Guid.NewGuid()}";
        IClinicalRegistrySiteIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-DEL", RegistryType.HIV));
        await grain.UpsertEntryAsync(MakeEntry("PAT-DEL", RegistryType.DiabetesMellitus));
        await grain.RemoveEntryAsync("PAT-DEL", RegistryType.HIV);
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].RegistryType, Is.EqualTo(RegistryType.DiabetesMellitus));
    }

    [Test]
    public async Task ClinicalRegistrySiteIndex_GetAllReturnsBothTypes()
    {
        string key = $"CCR-SITE-IDX-{Guid.NewGuid()}";
        IClinicalRegistrySiteIndexGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(key);
        await grain.UpsertEntryAsync(MakeEntry("PAT-H1", RegistryType.HIV));
        await grain.UpsertEntryAsync(MakeEntry("PAT-H2", RegistryType.HIV));
        await grain.UpsertEntryAsync(MakeEntry("PAT-D1", RegistryType.DiabetesMellitus));
        List<CCREntrySummary> all = await grain.GetAllEntriesAsync();
        Assert.That(all.Count(e => e.RegistryType == RegistryType.HIV), Is.EqualTo(2));
        Assert.That(all.Count(e => e.RegistryType == RegistryType.DiabetesMellitus), Is.EqualTo(1));
    }
}
