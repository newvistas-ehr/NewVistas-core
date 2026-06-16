// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Substance Abuse Treatment grain layer — RPMS CDMIS (File #9002170-9002174).
/// Tests episode, visit, and index grains directly via Orleans TestCluster.
/// </summary>
[TestFixture]
public class SubstanceAbuseTreatmentTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Episode grain — creation ────────────────────────────────────────────────

    [Test]
    public async Task EpisodeGrain_Create_PersistsAllFields()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        DateTime intake = new DateTime(2025, 1, 15);
        DateTime lastUse = new DateTime(2025, 1, 10);
        DateTime sobriety = new DateTime(2025, 1, 12);

        await grain.CreateAsync(
            patientId:          "PATIENT-001",
            modality:           SATreatmentModality.IntensiveOutpatient,
            primarySubstance:   SubstanceType.Alcohol,
            secondarySubstances: null,
            intakeDate:         intake,
            lastUseDate:        lastUse,
            sobrietyDate:       sobriety,
            programName:        "IOP-ALCOHOL",
            treatmentGoals:     new List<string> { "Maintain sobriety", "Attend AA meetings" },
            providerId:         "PROV-001",
            providerName:       "Dr. Smith",
            locationId:         "LOC-001",
            locationName:       "SA Treatment Center",
            notes:              "Initial intake assessment completed.");

        SATreatmentEpisodeState state = await grain.GetAsync();

        Assert.That(state.PatientId,        Is.EqualTo("PATIENT-001"));
        Assert.That(state.Status,           Is.EqualTo(SATreatmentStatus.Active));
        Assert.That(state.Modality,         Is.EqualTo(SATreatmentModality.IntensiveOutpatient));
        Assert.That(state.PrimarySubstance, Is.EqualTo(SubstanceType.Alcohol));
        Assert.That(state.IntakeDate,       Is.EqualTo(intake));
        Assert.That(state.LastUseDate,      Is.EqualTo(lastUse));
        Assert.That(state.SobrietyDate,     Is.EqualTo(sobriety));
        Assert.That(state.ProgramName,      Is.EqualTo("IOP-ALCOHOL"));
        Assert.That(state.TreatmentGoals,   Has.Count.EqualTo(2));
        Assert.That(state.ProviderName,     Is.EqualTo("Dr. Smith"));
        Assert.That(state.LocationName,     Is.EqualTo("SA Treatment Center"));
        Assert.That(state.Notes,            Does.Contain("intake assessment"));
    }

    [Test]
    public async Task EpisodeGrain_Create_WithSecondarySubstances()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-002",
            SATreatmentModality.ResidentialInpatient,
            SubstanceType.Opioids,
            new List<SubstanceType> { SubstanceType.Benzodiazepines, SubstanceType.Cannabis },
            DateTime.UtcNow,
            null, null,
            "RES-OPIOID", null,
            null, null, null, null,
            notes: null);

        SATreatmentEpisodeState state = await grain.GetAsync();

        Assert.That(state.PrimarySubstance,    Is.EqualTo(SubstanceType.Opioids));
        Assert.That(state.SecondarySubstances, Has.Count.EqualTo(2));
        Assert.That(state.SecondarySubstances, Contains.Item(SubstanceType.Benzodiazepines));
        Assert.That(state.SecondarySubstances, Contains.Item(SubstanceType.Cannabis));
    }

    // ── Episode grain — MAT ─────────────────────────────────────────────────────

    [Test]
    public async Task EpisodeGrain_AddMATEntry_Persists()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-003", SATreatmentModality.MedicationAssisted,
            SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        MATEntry mat = new MATEntry
        {
            EntryId        = $"MAT-{Guid.NewGuid()}",
            Medication     = MATMedication.BuprenorphineNaloxone,
            Dosage         = "8mg/2mg sublingual daily",
            StartDate      = DateTime.UtcNow,
            PrescriberId   = "PROV-010",
            PrescriberName = "Dr. Addiction Medicine",
            IsActive       = true,
            Notes          = "Induction phase"
        };

        await grain.AddMATEntryAsync(mat);

        SATreatmentEpisodeState state = await grain.GetAsync();

        Assert.That(state.MATEntries, Has.Count.EqualTo(1));
        Assert.That(state.MATEntries[0].Medication,     Is.EqualTo(MATMedication.BuprenorphineNaloxone));
        Assert.That(state.MATEntries[0].Dosage,         Is.EqualTo("8mg/2mg sublingual daily"));
        Assert.That(state.MATEntries[0].IsActive,       Is.True);
        Assert.That(state.MATEntries[0].PrescriberName, Is.EqualTo("Dr. Addiction Medicine"));
    }

    [Test]
    public async Task EpisodeGrain_AddDuplicateMAT_DoesNotAddTwice()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-004", SATreatmentModality.MedicationAssisted,
            SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string matId = $"MAT-{Guid.NewGuid()}";
        MATEntry mat = new MATEntry
        {
            EntryId    = matId,
            Medication = MATMedication.Methadone,
            Dosage     = "30mg daily",
            StartDate  = DateTime.UtcNow,
            IsActive   = true,
        };

        await grain.AddMATEntryAsync(mat);
        await grain.AddMATEntryAsync(mat);

        SATreatmentEpisodeState state = await grain.GetAsync();
        Assert.That(state.MATEntries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EpisodeGrain_StopMATEntry_SetsInactive()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-005", SATreatmentModality.MedicationAssisted,
            SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string matId = $"MAT-{Guid.NewGuid()}";
        await grain.AddMATEntryAsync(new MATEntry
        {
            EntryId    = matId,
            Medication = MATMedication.Naltrexone,
            Dosage     = "50mg daily",
            StartDate  = DateTime.UtcNow,
            IsActive   = true,
        });

        DateTime endDate = DateTime.UtcNow.AddDays(30);
        await grain.StopMATEntryAsync(matId, endDate);

        SATreatmentEpisodeState state = await grain.GetAsync();
        Assert.That(state.MATEntries[0].IsActive, Is.False);
        Assert.That(state.MATEntries[0].EndDate,  Is.EqualTo(endDate));
    }

    // ── Episode grain — treatment goals ─────────────────────────────────────────

    [Test]
    public async Task EpisodeGrain_AddTreatmentGoal_Appends()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-006", SATreatmentModality.Outpatient,
            SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await grain.AddTreatmentGoalAsync("Reduce alcohol use");
        await grain.AddTreatmentGoalAsync("Improve family relationships");
        await grain.AddTreatmentGoalAsync("Reduce alcohol use"); // duplicate — should not add

        SATreatmentEpisodeState state = await grain.GetAsync();
        Assert.That(state.TreatmentGoals, Has.Count.EqualTo(2));
        Assert.That(state.TreatmentGoals, Contains.Item("Reduce alcohol use"));
        Assert.That(state.TreatmentGoals, Contains.Item("Improve family relationships"));
    }

    // ── Episode grain — discharge & reopen ──────────────────────────────────────

    [Test]
    public async Task EpisodeGrain_Discharge_SetsStatusAndStopsMAT()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-007", SATreatmentModality.MedicationAssisted,
            SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string matId = $"MAT-{Guid.NewGuid()}";
        await grain.AddMATEntryAsync(new MATEntry
        {
            EntryId    = matId,
            Medication = MATMedication.Buprenorphine,
            Dosage     = "16mg daily",
            StartDate  = DateTime.UtcNow,
            IsActive   = true,
        });

        DateTime dischargeDate = DateTime.UtcNow.AddDays(90);
        await grain.DischargeAsync(dischargeDate, SADischargeDisposition.CompletedTreatment,
            "Successfully completed IOP program.");

        SATreatmentEpisodeState state = await grain.GetAsync();

        Assert.That(state.Status,               Is.EqualTo(SATreatmentStatus.Discharged));
        Assert.That(state.DischargeDate,        Is.EqualTo(dischargeDate));
        Assert.That(state.DischargeDisposition, Is.EqualTo(SADischargeDisposition.CompletedTreatment));
        Assert.That(state.MATEntries[0].IsActive, Is.False);
        Assert.That(state.MATEntries[0].EndDate,  Is.EqualTo(dischargeDate));
        Assert.That(state.Notes,                Does.Contain("Successfully completed"));
    }

    [Test]
    public async Task EpisodeGrain_Reopen_ClearsDischargeInfo()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-008", SATreatmentModality.Outpatient,
            SubstanceType.Stimulants, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await grain.DischargeAsync(DateTime.UtcNow, SADischargeDisposition.PatientDroppedOut, null);
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(SATreatmentStatus.Discharged));

        await grain.ReopenAsync("Patient returned for treatment.");

        SATreatmentEpisodeState state = await grain.GetAsync();
        Assert.That(state.Status,               Is.EqualTo(SATreatmentStatus.Reopened));
        Assert.That(state.DischargeDate,        Is.Null);
        Assert.That(state.DischargeDisposition, Is.Null);
        Assert.That(state.Notes,                Does.Contain("returned for treatment"));
    }

    [Test]
    public async Task EpisodeGrain_UpdateStatus_TransitionsCorrectly()
    {
        string id = $"SA-EPISODE:{Guid.NewGuid()}";
        ISATreatmentEpisodeGrain grain =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(id);

        await grain.CreateAsync(
            "PATIENT-009", SATreatmentModality.Outpatient,
            SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(SATreatmentStatus.Active));

        await grain.UpdateStatusAsync(SATreatmentStatus.Transferred);
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(SATreatmentStatus.Transferred));

        await grain.UpdateStatusAsync(SATreatmentStatus.Closed);
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(SATreatmentStatus.Closed));
    }

    // ── Episode index grain ─────────────────────────────────────────────────────

    [Test]
    public async Task EpisodeIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        string indexKey = $"SA-EPISODE-IDX:PATIENT-{Guid.NewGuid()}";
        ISATreatmentEpisodeIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeIndexGrain>(indexKey);

        string id1 = $"SA-EPISODE:{Guid.NewGuid()}";
        string id2 = $"SA-EPISODE:{Guid.NewGuid()}";

        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = id1,
            PatientId        = "P-001",
            Status           = SATreatmentStatus.Discharged,
            Modality         = SATreatmentModality.Outpatient,
            PrimarySubstance = SubstanceType.Alcohol,
            IntakeDate       = new DateTime(2024, 1, 15),
            DischargeDate    = new DateTime(2024, 6, 15),
        });

        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = id2,
            PatientId        = "P-001",
            Status           = SATreatmentStatus.Active,
            Modality         = SATreatmentModality.IntensiveOutpatient,
            PrimarySubstance = SubstanceType.Opioids,
            IntakeDate       = new DateTime(2024, 8, 1),
        });

        List<SATreatmentEpisodeIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].EpisodeId, Is.EqualTo(id2));
        Assert.That(all[1].EpisodeId, Is.EqualTo(id1));
    }

    [Test]
    public async Task EpisodeIndexGrain_GetActive_ReturnsActiveOrReopened()
    {
        string indexKey = $"SA-EPISODE-IDX:PATIENT-{Guid.NewGuid()}";
        ISATreatmentEpisodeIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeIndexGrain>(indexKey);

        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = $"SA-EPISODE:{Guid.NewGuid()}",
            PatientId        = "P-002",
            Status           = SATreatmentStatus.Discharged,
            Modality         = SATreatmentModality.Outpatient,
            PrimarySubstance = SubstanceType.Alcohol,
            IntakeDate       = DateTime.UtcNow.AddDays(-180),
        });

        string activeId = $"SA-EPISODE:{Guid.NewGuid()}";
        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = activeId,
            PatientId        = "P-002",
            Status           = SATreatmentStatus.Active,
            Modality         = SATreatmentModality.MedicationAssisted,
            PrimarySubstance = SubstanceType.Opioids,
            IntakeDate       = DateTime.UtcNow,
        });

        SATreatmentEpisodeIndexEntry? active = await index.GetActiveAsync();

        Assert.That(active, Is.Not.Null);
        Assert.That(active!.EpisodeId, Is.EqualTo(activeId));
        Assert.That(active.Status, Is.EqualTo(SATreatmentStatus.Active));
    }

    [Test]
    public async Task EpisodeIndexGrain_GetByStatus_FiltersCorrectly()
    {
        string indexKey = $"SA-EPISODE-IDX:PATIENT-{Guid.NewGuid()}";
        ISATreatmentEpisodeIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeIndexGrain>(indexKey);

        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = $"SA-EPISODE:{Guid.NewGuid()}",
            PatientId        = "P-003",
            Status           = SATreatmentStatus.Active,
            Modality         = SATreatmentModality.Outpatient,
            PrimarySubstance = SubstanceType.Cannabis,
            IntakeDate       = DateTime.UtcNow,
        });

        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = $"SA-EPISODE:{Guid.NewGuid()}",
            PatientId        = "P-003",
            Status           = SATreatmentStatus.Discharged,
            Modality         = SATreatmentModality.Detoxification,
            PrimarySubstance = SubstanceType.Opioids,
            IntakeDate       = DateTime.UtcNow.AddDays(-90),
            DischargeDate    = DateTime.UtcNow.AddDays(-30),
        });

        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = $"SA-EPISODE:{Guid.NewGuid()}",
            PatientId        = "P-003",
            Status           = SATreatmentStatus.Discharged,
            Modality         = SATreatmentModality.ResidentialInpatient,
            PrimarySubstance = SubstanceType.Alcohol,
            IntakeDate       = DateTime.UtcNow.AddDays(-365),
            DischargeDate    = DateTime.UtcNow.AddDays(-270),
        });

        List<SATreatmentEpisodeIndexEntry> active =
            await index.GetByStatusAsync(SATreatmentStatus.Active);
        List<SATreatmentEpisodeIndexEntry> discharged =
            await index.GetByStatusAsync(SATreatmentStatus.Discharged);

        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(discharged, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EpisodeIndexGrain_UpdateEntry_ChangesStatusAndDate()
    {
        string indexKey = $"SA-EPISODE-IDX:PATIENT-{Guid.NewGuid()}";
        ISATreatmentEpisodeIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISATreatmentEpisodeIndexGrain>(indexKey);

        string episodeId = $"SA-EPISODE:{Guid.NewGuid()}";
        await index.AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = episodeId,
            PatientId        = "P-004",
            Status           = SATreatmentStatus.Active,
            Modality         = SATreatmentModality.Outpatient,
            PrimarySubstance = SubstanceType.Alcohol,
            IntakeDate       = DateTime.UtcNow,
        });

        DateTime dischargeDate = DateTime.UtcNow.AddDays(60);
        await index.UpdateEntryAsync(episodeId, SATreatmentStatus.Discharged, dischargeDate);

        List<SATreatmentEpisodeIndexEntry> all = await index.GetAllAsync();
        Assert.That(all[0].Status,        Is.EqualTo(SATreatmentStatus.Discharged));
        Assert.That(all[0].DischargeDate, Is.EqualTo(dischargeDate));

        SATreatmentEpisodeIndexEntry? active = await index.GetActiveAsync();
        Assert.That(active, Is.Null);
    }

    // ── Visit grain — creation ──────────────────────────────────────────────────

    [Test]
    public async Task VisitGrain_Create_PersistsAllFields()
    {
        string id = $"SA-VISIT:{Guid.NewGuid()}";
        ISAVisitGrain grain =
            _cluster.GrainFactory.GetGrain<ISAVisitGrain>(id);

        DateTime visitDate = new DateTime(2025, 2, 10, 14, 0, 0);
        string episodeId = $"SA-EPISODE:{Guid.NewGuid()}";

        await grain.CreateAsync(
            episodeId:              episodeId,
            patientId:              "PATIENT-010",
            visitDate:              visitDate,
            visitType:              SAVisitType.Individual,
            durationMinutes:        60,
            udsResult:              null,
            udsSubstancesDetected:  null,
            daysSinceLastUse:       30,
            cravingLevel:           3,
            providerId:             "PROV-020",
            providerName:           "Dr. Counselor",
            notes:                  "Patient reports decreased cravings.");

        SAVisitState state = await grain.GetAsync();

        Assert.That(state.EpisodeId,       Is.EqualTo(episodeId));
        Assert.That(state.PatientId,       Is.EqualTo("PATIENT-010"));
        Assert.That(state.VisitDate,       Is.EqualTo(visitDate));
        Assert.That(state.VisitType,       Is.EqualTo(SAVisitType.Individual));
        Assert.That(state.DurationMinutes, Is.EqualTo(60));
        Assert.That(state.DaysSinceLastUse, Is.EqualTo(30));
        Assert.That(state.CravingLevel,    Is.EqualTo(3));
        Assert.That(state.ProviderName,    Is.EqualTo("Dr. Counselor"));
        Assert.That(state.Notes,           Does.Contain("decreased cravings"));
    }

    [Test]
    public async Task VisitGrain_Create_UDS_WithSubstancesDetected()
    {
        string id = $"SA-VISIT:{Guid.NewGuid()}";
        ISAVisitGrain grain =
            _cluster.GrainFactory.GetGrain<ISAVisitGrain>(id);

        await grain.CreateAsync(
            episodeId:              $"SA-EPISODE:{Guid.NewGuid()}",
            patientId:              "PATIENT-011",
            visitDate:              DateTime.UtcNow,
            visitType:              SAVisitType.UrineDrugScreen,
            durationMinutes:        15,
            udsResult:              "POSITIVE",
            udsSubstancesDetected:  new List<string> { "Opioids", "Benzodiazepines" },
            daysSinceLastUse:       null,
            cravingLevel:           null,
            providerId:             null,
            providerName:           "Lab Tech",
            notes:                  "Unexpected positive — counselor notified.");

        SAVisitState state = await grain.GetAsync();

        Assert.That(state.VisitType,              Is.EqualTo(SAVisitType.UrineDrugScreen));
        Assert.That(state.UdsResult,              Is.EqualTo("POSITIVE"));
        Assert.That(state.UdsSubstancesDetected,  Has.Count.EqualTo(2));
        Assert.That(state.UdsSubstancesDetected,  Contains.Item("Opioids"));
        Assert.That(state.UdsSubstancesDetected,  Contains.Item("Benzodiazepines"));
    }

    [Test]
    public async Task VisitGrain_Create_WithCravingLevel()
    {
        string id = $"SA-VISIT:{Guid.NewGuid()}";
        ISAVisitGrain grain =
            _cluster.GrainFactory.GetGrain<ISAVisitGrain>(id);

        await grain.CreateAsync(
            $"SA-EPISODE:{Guid.NewGuid()}",
            "PATIENT-012",
            DateTime.UtcNow,
            SAVisitType.MedicationCheck,
            30,
            null, null,
            daysSinceLastUse: 45,
            cravingLevel: 7,
            null, "Dr. MAT Provider",
            "High craving reported — adjusted dosage.");

        SAVisitState state = await grain.GetAsync();

        Assert.That(state.CravingLevel,     Is.EqualTo(7));
        Assert.That(state.DaysSinceLastUse, Is.EqualTo(45));
        Assert.That(state.VisitType,        Is.EqualTo(SAVisitType.MedicationCheck));
    }

    // ── Visit index grain ───────────────────────────────────────────────────────

    [Test]
    public async Task VisitIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        string indexKey = $"SA-VISIT-IDX:EPISODE-{Guid.NewGuid()}";
        ISAVisitIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISAVisitIndexGrain>(indexKey);

        string v1 = $"SA-VISIT:{Guid.NewGuid()}";
        string v2 = $"SA-VISIT:{Guid.NewGuid()}";
        string v3 = $"SA-VISIT:{Guid.NewGuid()}";

        await index.AddEntryAsync(new SAVisitIndexEntry
        {
            VisitId         = v1,
            EpisodeId       = "EP-001",
            VisitDate       = new DateTime(2025, 1, 10),
            VisitType       = SAVisitType.Individual,
            DurationMinutes = 60,
            ProviderName    = "Dr. A",
        });

        await index.AddEntryAsync(new SAVisitIndexEntry
        {
            VisitId         = v2,
            EpisodeId       = "EP-001",
            VisitDate       = new DateTime(2025, 1, 17),
            VisitType       = SAVisitType.Group,
            DurationMinutes = 90,
            ProviderName    = "Dr. B",
        });

        await index.AddEntryAsync(new SAVisitIndexEntry
        {
            VisitId         = v3,
            EpisodeId       = "EP-001",
            VisitDate       = new DateTime(2025, 1, 24),
            VisitType       = SAVisitType.UrineDrugScreen,
            DurationMinutes = 15,
            UdsResult       = "NEGATIVE",
        });

        List<SAVisitIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(3));
        Assert.That(all[0].VisitId, Is.EqualTo(v3));
        Assert.That(all[2].VisitId, Is.EqualTo(v1));
    }

    [Test]
    public async Task VisitIndexGrain_GetVisitCount_ReturnsCorrectCount()
    {
        string indexKey = $"SA-VISIT-IDX:EPISODE-{Guid.NewGuid()}";
        ISAVisitIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISAVisitIndexGrain>(indexKey);

        Assert.That(await index.GetVisitCountAsync(), Is.EqualTo(0));

        await index.AddEntryAsync(new SAVisitIndexEntry
        {
            VisitId   = $"SA-VISIT:{Guid.NewGuid()}",
            EpisodeId = "EP-002",
            VisitDate = DateTime.UtcNow,
            VisitType = SAVisitType.Individual,
        });

        await index.AddEntryAsync(new SAVisitIndexEntry
        {
            VisitId   = $"SA-VISIT:{Guid.NewGuid()}",
            EpisodeId = "EP-002",
            VisitDate = DateTime.UtcNow.AddDays(7),
            VisitType = SAVisitType.Group,
        });

        Assert.That(await index.GetVisitCountAsync(), Is.EqualTo(2));
    }
}
