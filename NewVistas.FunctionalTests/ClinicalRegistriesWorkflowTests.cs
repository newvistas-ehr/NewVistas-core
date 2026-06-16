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
/// Functional tests for VistA Clinical Case Registries (HIV, HepC, Diabetes).
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end registry enrollment and condition-specific data updates.
/// </summary>
[TestFixture]
public class ClinicalRegistriesWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IClinicalRegistryEntryGrain GetEntryGrain(RegistryType type, string patientId) =>
        _cluster.GrainFactory.GetGrain<IClinicalRegistryEntryGrain>($"CCR:{type}:{patientId}");

    private IClinicalRegistryIndexGrain GetRegistryIndex(RegistryType type) =>
        _cluster.GrainFactory.GetGrain<IClinicalRegistryIndexGrain>($"CCR-IDX:{type}");

    private IPatientRegistryListGrain GetPatientList(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientRegistryListGrain>($"CCR-PAT:{patientId}");

    private IClinicalRegistrySiteIndexGrain GetSiteIndex() =>
        _cluster.GrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>("CCR-SITE-IDX");

    private static async Task EnrollHIVPatient(IClinicalRegistryEntryGrain grain, string patientId)
    {
        await grain.EnrollPatientAsync(
            patientId, "HIV Patient", new DateTime(1965, 3, 10),
            RegistryType.HIV, "PRV-001", "Dr. Infectious",
            "SITE-001", "VA Medical Center",
            "PRV-002", "Dr. Primary",
            "Enrolled from ID clinic");
    }

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HIVRegistry_Enroll_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.HIV, patientId);

        await EnrollHIVPatient(grain, patientId);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("HIV Patient"));
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.HIV));
        Assert.That(state.EnrollmentStatus, Is.EqualTo(CCREnrollmentStatus.Active));
        Assert.That(state.PrimaryProviderName, Is.EqualTo("Dr. Primary"));
        Assert.That(state.SiteId, Is.EqualTo("SITE-001"));
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HIVRegistry_UpdateHIVData_PersistsViralLoadAndCD4()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.HIV, patientId);
        await EnrollHIVPatient(grain, patientId);

        await grain.UpdateHIVDataAsync(
            HIVStage.Stage1, 650m, DateTime.UtcNow.AddDays(-7),
            20m, DateTime.UtcNow.AddDays(-7),
            true, new DateTime(2020, 1, 15),
            "TDF/FTC/DTG");

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.HIVStage, Is.EqualTo(HIVStage.Stage1));
        Assert.That(state.CD4CountCellsPerMm3, Is.EqualTo(650m));
        Assert.That(state.ViralLoadCopiesPerMl, Is.EqualTo(20m));
        Assert.That(state.IsVirallySuppressed, Is.True);
        Assert.That(state.CurrentARTRegimen, Is.EqualTo("TDF/FTC/DTG"));
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HepCRegistry_Enroll_AndUpdateData()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.HepatitisCVirus, patientId);

        await grain.EnrollPatientAsync(
            patientId, "HepC Patient", new DateTime(1970, 8, 20),
            RegistryType.HepatitisCVirus, "PRV-001", "Dr. Hepatologist",
            "SITE-001", "VA Liver Clinic",
            "PRV-003", "Dr. Gastro",
            null);

        await grain.UpdateHepCDataAsync(
            HepCGenotype.Genotype1a, 12.5m,
            HepCTreatmentStatus.CurrentlyOnTreatment,
            DateTime.UtcNow.AddDays(-30), null,
            false, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.HepatitisCVirus));
        Assert.That(state.HepCGenotype, Is.EqualTo(HepCGenotype.Genotype1a));
        Assert.That(state.FibrosisScoreKpa, Is.EqualTo(12.5m));
        Assert.That(state.HepCTreatmentStatus, Is.EqualTo(HepCTreatmentStatus.CurrentlyOnTreatment));
        Assert.That(state.SVRAchieved, Is.False);
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HepCRegistry_UpdateSVR_Achieved()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.HepatitisCVirus, patientId);
        await grain.EnrollPatientAsync(
            patientId, "SVR Patient", null,
            RegistryType.HepatitisCVirus, "PRV-001", "Dr. A",
            "SITE-001", "VA", "PRV-002", "Dr. B", null);

        await grain.UpdateHepCDataAsync(
            HepCGenotype.Genotype2, 8.0m,
            HepCTreatmentStatus.SVRAchieved,
            DateTime.UtcNow.AddDays(-180), DateTime.UtcNow.AddDays(-90),
            true, DateTime.UtcNow.AddDays(-30));

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.SVRAchieved, Is.True);
        Assert.That(state.SVRDate, Is.Not.Null);
        Assert.That(state.HepCTreatmentStatus, Is.EqualTo(HepCTreatmentStatus.SVRAchieved));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DiabetesRegistry_Enroll_AndUpdateData()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);

        await grain.EnrollPatientAsync(
            patientId, "Diabetes Patient", new DateTime(1960, 11, 5),
            RegistryType.DiabetesMellitus, "PRV-001", "Dr. Endocrine",
            "SITE-001", "VA Diabetes Clinic",
            "PRV-004", "Dr. Primary",
            null);

        await grain.UpdateDiabetesDataAsync(
            DiabetesType.Type2, 8.5m, DateTime.UtcNow.AddDays(-14),
            false, new List<string> { "Retinopathy", "Nephropathy" });

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.DiabetesMellitus));
        Assert.That(state.DiabetesType, Is.EqualTo(DiabetesType.Type2));
        Assert.That(state.HbA1cPct, Is.EqualTo(8.5m));
        Assert.That(state.IsInsulinDependent, Is.False);
        Assert.That(state.DiabetesComplications, Has.Count.EqualTo(2));
        Assert.That(state.DiabetesComplications, Contains.Item("Retinopathy"));
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Registry_UpdateEnrollmentStatus_ToInactive()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.HIV, patientId);
        await EnrollHIVPatient(grain, patientId);

        await grain.UpdateEnrollmentStatusAsync(
            CCREnrollmentStatus.Inactive, DateTime.UtcNow, "Patient relocated");

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(CCREnrollmentStatus.Inactive));
        Assert.That(state.DeactivationDate, Is.Not.Null);
        Assert.That(state.DeactivationReason, Is.EqualTo("Patient relocated"));
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task RegistryIndex_UpsertAndGetAll()
    {
        IClinicalRegistryIndexGrain index = GetRegistryIndex(RegistryType.HIV);

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await index.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = patientId,
            PatientName = "Index HIV Patient",
            RegistryType = RegistryType.HIV,
            Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            SiteId = "SITE-001",
            PrimaryProviderName = "Dr. Primary",
            LastModifiedDate = DateTime.UtcNow
        });

        List<CCREntrySummary> all = await index.GetAllEntriesAsync();
        Assert.That(all.Any(e => e.PatientId == patientId), Is.True);
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task RegistryIndex_GetActiveEntries_FiltersCorrectly()
    {
        IClinicalRegistryIndexGrain index = GetRegistryIndex(RegistryType.DiabetesMellitus);

        string activePatient = $"PAT-{Guid.NewGuid():N}";
        string inactivePatient = $"PAT-{Guid.NewGuid():N}";

        await index.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = activePatient, PatientName = "Active DM",
            RegistryType = RegistryType.DiabetesMellitus, Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow, SiteId = "SITE-001", PrimaryProviderName = "Dr. A",
            LastModifiedDate = DateTime.UtcNow
        });
        await index.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = inactivePatient, PatientName = "Inactive DM",
            RegistryType = RegistryType.DiabetesMellitus, Status = CCREnrollmentStatus.Inactive,
            EnrollmentDate = DateTime.UtcNow, SiteId = "SITE-001", PrimaryProviderName = "Dr. B",
            LastModifiedDate = DateTime.UtcNow
        });

        List<CCREntrySummary> active = await index.GetActiveEntriesAsync();
        Assert.That(active.Any(e => e.PatientId == activePatient), Is.True);
        Assert.That(active.All(e => e.Status == CCREnrollmentStatus.Active), Is.True);
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientRegistryList_UpsertAndGetAll()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPatientRegistryListGrain list = GetPatientList(patientId);

        await list.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.HIV,
            Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            PrimaryProviderName = "Dr. Primary"
        });
        await list.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.DiabetesMellitus,
            Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            PrimaryProviderName = "Dr. Endocrine"
        });

        List<PatientRegistryEnrollmentEntry> all = await list.GetAllEnrollmentsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientRegistryList_GetActive_FiltersCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPatientRegistryListGrain list = GetPatientList(patientId);

        await list.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.HIV, Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow, LastModifiedDate = DateTime.UtcNow, PrimaryProviderName = "Dr. A"
        });
        await list.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.HepatitisCVirus, Status = CCREnrollmentStatus.Inactive,
            EnrollmentDate = DateTime.UtcNow, LastModifiedDate = DateTime.UtcNow, PrimaryProviderName = "Dr. B"
        });

        List<PatientRegistryEnrollmentEntry> active = await list.GetActiveEnrollmentsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].RegistryType, Is.EqualTo(RegistryType.HIV));
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SiteIndex_UpsertAndGetAll()
    {
        IClinicalRegistrySiteIndexGrain siteIndex = GetSiteIndex();

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await siteIndex.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = patientId, PatientName = "Site Index Patient",
            RegistryType = RegistryType.HIV, Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow, SiteId = "SITE-001",
            PrimaryProviderName = "Dr. A", LastModifiedDate = DateTime.UtcNow
        });

        List<CCREntrySummary> all = await siteIndex.GetAllEntriesAsync();
        Assert.That(all.Any(e => e.PatientId == patientId), Is.True);
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task EndToEnd_EnrollPatientInHIVAndIndexAcrossAllGrains()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";

        // Enroll in HIV registry
        IClinicalRegistryEntryGrain entry = GetEntryGrain(RegistryType.HIV, patientId);
        await entry.EnrollPatientAsync(
            patientId, "E2E Patient", new DateTime(1975, 7, 4),
            RegistryType.HIV, "PRV-E2E", "Dr. E2E",
            "SITE-001", "VA Medical Center",
            "PRV-PRI", "Dr. Primary", null);

        // Update HIV data
        await entry.UpdateHIVDataAsync(
            HIVStage.Stage2, 450m, DateTime.UtcNow,
            50m, DateTime.UtcNow,
            true, DateTime.UtcNow.AddYears(-3), "BIC/FTC/TAF");

        ClinicalRegistryEntryState state = await entry.GetEntryAsync();
        Assert.That(state.HIVStage, Is.EqualTo(HIVStage.Stage2));
        Assert.That(state.IsVirallySuppressed, Is.True);

        // Update per-patient list
        IPatientRegistryListGrain patList = GetPatientList(patientId);
        await patList.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.HIV, Status = CCREnrollmentStatus.Active,
            EnrollmentDate = state.EnrollmentDate, LastModifiedDate = DateTime.UtcNow,
            PrimaryProviderName = state.PrimaryProviderName
        });

        // Update registry type index
        IClinicalRegistryIndexGrain regIndex = GetRegistryIndex(RegistryType.HIV);
        await regIndex.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = patientId, PatientName = "E2E Patient",
            RegistryType = RegistryType.HIV, Status = CCREnrollmentStatus.Active,
            EnrollmentDate = state.EnrollmentDate, SiteId = "SITE-001",
            PrimaryProviderName = "Dr. Primary", LastModifiedDate = DateTime.UtcNow
        });

        // Update site index
        IClinicalRegistrySiteIndexGrain siteIndex = GetSiteIndex();
        await siteIndex.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = patientId, PatientName = "E2E Patient",
            RegistryType = RegistryType.HIV, Status = CCREnrollmentStatus.Active,
            EnrollmentDate = state.EnrollmentDate, SiteId = "SITE-001",
            PrimaryProviderName = "Dr. Primary", LastModifiedDate = DateTime.UtcNow
        });

        // Verify across all grains
        List<PatientRegistryEnrollmentEntry> patEnrollments = await patList.GetAllEnrollmentsAsync();
        Assert.That(patEnrollments, Has.Count.EqualTo(1));

        List<CCREntrySummary> regEntries = await regIndex.GetAllEntriesAsync();
        Assert.That(regEntries.Any(e => e.PatientId == patientId), Is.True);

        List<CCREntrySummary> siteEntries = await siteIndex.GetAllEntriesAsync();
        Assert.That(siteEntries.Any(e => e.PatientId == patientId), Is.True);
    }
}
