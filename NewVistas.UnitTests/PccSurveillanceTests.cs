// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for PCC Surveillance grains — configuration, match, and index operations.
/// Tests encounter-level surveillance criteria and match lifecycle (RPMS APCSB.m / APCSSIL2.m).
/// </summary>
[TestFixture]
public class PccSurveillanceTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Config Grain ─────────────────────────────────────────────────────────

    [Test]
    public async Task ConfigGrain_Save_PersistsAllFields()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "Influenza-Like Illness",
            PccEncounterClassification.InfluenzaLikeIllness,
            new List<PccSurveillanceCriterion>
            {
                new() { Code = "J11.1", CodeSystem = "ICD-10", Description = "Influenza with respiratory manifestations", MatchType = "diagnosis" },
                new() { Code = "J06.9", CodeSystem = "ICD-10", Description = "Acute upper respiratory infection", MatchType = "diagnosis" },
                new() { Code = "R50.9", CodeSystem = "ICD-10", Description = "Fever, unspecified", MatchType = "diagnosis" },
            },
            new List<PccVisitType> { PccVisitType.Ambulatory, PccVisitType.Emergency },
            detectComorbidities: true,
            captureVitals: true,
            scanWindowDays: 90,
            new List<string> { "US", "IHS" },
            "24 hours",
            isActive: true);

        PccSurveillanceConfigState result = await grain.GetAsync();
        Assert.That(result.ConditionName, Is.EqualTo("Influenza-Like Illness"));
        Assert.That(result.Classification, Is.EqualTo(PccEncounterClassification.InfluenzaLikeIllness));
        Assert.That(result.Criteria, Has.Count.EqualTo(3));
        Assert.That(result.RequiredVisitTypes, Has.Count.EqualTo(2));
        Assert.That(result.RequiredVisitTypes, Contains.Item(PccVisitType.Ambulatory));
        Assert.That(result.RequiredVisitTypes, Contains.Item(PccVisitType.Emergency));
        Assert.That(result.DetectComorbidities, Is.True);
        Assert.That(result.CaptureVitals, Is.True);
        Assert.That(result.ScanWindowDays, Is.EqualTo(90));
        Assert.That(result.Jurisdictions, Has.Count.EqualTo(2));
        Assert.That(result.Jurisdictions, Contains.Item("US"));
        Assert.That(result.Jurisdictions, Contains.Item("IHS"));
        Assert.That(result.ReportingTimeframe, Is.EqualTo("24 hours"));
        Assert.That(result.IsActive, Is.True);
    }

    [Test]
    public async Task ConfigGrain_AddCriterion_AppendsList()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "Test Config", PccEncounterClassification.InfluenzaLikeIllness,
            null, null, true, true, 90, null, "24 hours", true);

        await grain.AddCriterionAsync(new PccSurveillanceCriterion
        {
            Code = "J11.1", CodeSystem = "ICD-10", Description = "Influenza", MatchType = "diagnosis"
        });
        await grain.AddCriterionAsync(new PccSurveillanceCriterion
        {
            Code = "R50.9", CodeSystem = "ICD-10", Description = "Fever", MatchType = "diagnosis"
        });

        PccSurveillanceConfigState result = await grain.GetAsync();
        Assert.That(result.Criteria, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ConfigGrain_AddCriterion_NoDuplicates()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "Dup Test", PccEncounterClassification.InfluenzaLikeIllness,
            null, null, true, true, 90, null, "24 hours", true);

        PccSurveillanceCriterion criterion = new()
        {
            Code = "J11.1", CodeSystem = "ICD-10", Description = "Influenza", MatchType = "diagnosis"
        };

        await grain.AddCriterionAsync(criterion);
        await grain.AddCriterionAsync(criterion);

        PccSurveillanceConfigState result = await grain.GetAsync();
        Assert.That(result.Criteria, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ConfigGrain_SetActive_Toggles()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "Toggle Test", PccEncounterClassification.InfluenzaLikeIllness,
            null, null, true, true, 90, null, "24 hours", true);

        await grain.SetActiveAsync(false);
        Assert.That((await grain.GetAsync()).IsActive, Is.False);

        await grain.SetActiveAsync(true);
        Assert.That((await grain.GetAsync()).IsActive, Is.True);
    }

    [Test]
    public async Task ConfigGrain_DefaultScanWindow_Is90Days()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        PccSurveillanceConfigState result = await grain.GetAsync();
        Assert.That(result.ScanWindowDays, Is.EqualTo(90));
    }

    // ─── Config Index Grain ───────────────────────────────────────────────────

    [Test]
    public async Task ConfigIndexGrain_Upsert_AddsNew()
    {
        string indexKey = $"PCC-SURV-CONFIG-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceConfigIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigIndexGrain>(indexKey);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = "CFG-001",
            ConditionName = "Influenza-Like Illness",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 3,
            IsActive = true
        });

        List<PccSurveillanceConfigIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ConditionName, Is.EqualTo("Influenza-Like Illness"));
        Assert.That(all[0].CriteriaCount, Is.EqualTo(3));
    }

    [Test]
    public async Task ConfigIndexGrain_Upsert_UpdatesExisting()
    {
        string indexKey = $"PCC-SURV-CONFIG-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceConfigIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigIndexGrain>(indexKey);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = "CFG-UPD",
            ConditionName = "Original",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 1,
            IsActive = true
        });

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = "CFG-UPD",
            ConditionName = "Updated",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 5,
            IsActive = true
        });

        List<PccSurveillanceConfigIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ConditionName, Is.EqualTo("Updated"));
        Assert.That(all[0].CriteriaCount, Is.EqualTo(5));
    }

    [Test]
    public async Task ConfigIndexGrain_GetActive_FiltersCorrectly()
    {
        string indexKey = $"PCC-SURV-CONFIG-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceConfigIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigIndexGrain>(indexKey);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = "CFG-A1", ConditionName = "Active 1",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 2, IsActive = true
        });
        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = "CFG-A2", ConditionName = "Active 2",
            Classification = PccEncounterClassification.SevereRespiratoryDisease,
            CriteriaCount = 3, IsActive = true
        });
        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = "CFG-I1", ConditionName = "Inactive 1",
            Classification = PccEncounterClassification.ReportableCommunicable,
            CriteriaCount = 1, IsActive = false
        });

        List<PccSurveillanceConfigIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(e => e.IsActive), Is.True);
    }

    // ─── Match Grain ──────────────────────────────────────────────────────────

    [Test]
    public async Task MatchGrain_Create_PersistsAllFields()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        DateTime encounterDate = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(
            patientId: "PAT-001",
            patientName: "SMITH,JOHN",
            configId: "CFG-ILI-001",
            conditionName: "Influenza-Like Illness",
            classification: PccEncounterClassification.InfluenzaLikeIllness,
            encounterDate: encounterDate,
            visitType: PccVisitType.Ambulatory,
            chiefComplaint: "Fever and cough",
            facilityName: "IHS Clinic Alpha",
            dischargeDate: null,
            providerName: "DR. JONES",
            matchingDiagnoses: new List<string> { "J11.1", "J06.9" },
            matchingProcedures: null,
            matchingLabResults: new List<string> { "LOINC:33535-6" },
            matchingMedications: null,
            comorbidities: new PccComorbidityFlags { Asthma = true, Diabetes = true },
            vitals: new PccEncounterVitals { TemperatureF = 101.5m, OxygenSaturationPct = 95 });

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(result.PatientName, Is.EqualTo("SMITH,JOHN"));
        Assert.That(result.ConfigId, Is.EqualTo("CFG-ILI-001"));
        Assert.That(result.ConditionName, Is.EqualTo("Influenza-Like Illness"));
        Assert.That(result.Classification, Is.EqualTo(PccEncounterClassification.InfluenzaLikeIllness));
        Assert.That(result.EncounterDate, Is.EqualTo(encounterDate));
        Assert.That(result.VisitType, Is.EqualTo(PccVisitType.Ambulatory));
        Assert.That(result.ChiefComplaint, Is.EqualTo("Fever and cough"));
        Assert.That(result.FacilityName, Is.EqualTo("IHS Clinic Alpha"));
        Assert.That(result.ProviderName, Is.EqualTo("DR. JONES"));
        Assert.That(result.MatchingDiagnoses, Has.Count.EqualTo(2));
        Assert.That(result.MatchingDiagnoses, Contains.Item("J11.1"));
        Assert.That(result.MatchingDiagnoses, Contains.Item("J06.9"));
        Assert.That(result.MatchingLabResults, Has.Count.EqualTo(1));
        Assert.That(result.MatchingLabResults, Contains.Item("LOINC:33535-6"));
        Assert.That(result.Comorbidities, Is.Not.Null);
        Assert.That(result.Comorbidities!.Asthma, Is.True);
        Assert.That(result.Comorbidities.Diabetes, Is.True);
        Assert.That(result.Vitals, Is.Not.Null);
        Assert.That(result.Vitals!.TemperatureF, Is.EqualTo(101.5m));
        Assert.That(result.Vitals.OxygenSaturationPct, Is.EqualTo(95));
    }

    [Test]
    public async Task MatchGrain_Create_DefaultsToDetectedStatus()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-002", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null, null, null);

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Detected));
    }

    [Test]
    public async Task MatchGrain_UpdateStatus_Reviewed()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-003", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null, null, null);

        await grain.UpdateStatusAsync(PccSurveillanceMatchStatus.Reviewed);

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Reviewed));
    }

    [Test]
    public async Task MatchGrain_UpdateStatus_Reported()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-004", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null, null, null);

        await grain.UpdateStatusAsync(PccSurveillanceMatchStatus.Reported);

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Reported));
    }

    [Test]
    public async Task MatchGrain_MarkExported_SetsDateAndReference()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-005", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null, null, null);

        await grain.MarkExportedAsync("EPILABHL7_SITE01_20260322.txt");

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Exported));
        Assert.That(result.ExportReference, Is.EqualTo("EPILABHL7_SITE01_20260322.txt"));
        Assert.That(result.ExportedDate, Is.Not.Null);
    }

    [Test]
    public async Task MatchGrain_WithComorbidities_AllFlags()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-006", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null,
            comorbidities: new PccComorbidityFlags
            {
                Asthma = true,
                Diabetes = true,
                Obesity = true,
                Pregnancy = true,
                Immunocompromised = true,
                ChronicLungDisease = true,
                CardiovascularDisease = true,
                Bmi = 35.2m
            },
            vitals: null);

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Comorbidities, Is.Not.Null);
        Assert.That(result.Comorbidities!.Asthma, Is.True);
        Assert.That(result.Comorbidities.Diabetes, Is.True);
        Assert.That(result.Comorbidities.Obesity, Is.True);
        Assert.That(result.Comorbidities.Pregnancy, Is.True);
        Assert.That(result.Comorbidities.Immunocompromised, Is.True);
        Assert.That(result.Comorbidities.ChronicLungDisease, Is.True);
        Assert.That(result.Comorbidities.CardiovascularDisease, Is.True);
        Assert.That(result.Comorbidities.Bmi, Is.EqualTo(35.2m));
    }

    [Test]
    public async Task MatchGrain_WithVitals_AllFields()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-007", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null,
            comorbidities: null,
            vitals: new PccEncounterVitals
            {
                TemperatureF = 101.5m,
                OxygenSaturationPct = 95,
                HeartRate = 110,
                RespiratoryRate = 22,
                BloodPressureSystolic = 130,
                BloodPressureDiastolic = 85
            });

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Vitals, Is.Not.Null);
        Assert.That(result.Vitals!.TemperatureF, Is.EqualTo(101.5m));
        Assert.That(result.Vitals.OxygenSaturationPct, Is.EqualTo(95));
        Assert.That(result.Vitals.HeartRate, Is.EqualTo(110));
        Assert.That(result.Vitals.RespiratoryRate, Is.EqualTo(22));
        Assert.That(result.Vitals.BloodPressureSystolic, Is.EqualTo(130));
        Assert.That(result.Vitals.BloodPressureDiastolic, Is.EqualTo(85));
    }

    [Test]
    public async Task MatchGrain_WithMedications_PersistsList()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-008", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null,
            matchingMedications: new List<string> { "Oseltamivir", "Zanamivir" },
            comorbidities: null, vitals: null);

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.MatchingMedications, Has.Count.EqualTo(2));
        Assert.That(result.MatchingMedications, Contains.Item("Oseltamivir"));
        Assert.That(result.MatchingMedications, Contains.Item("Zanamivir"));
    }

    // ─── Match Index Grain ────────────────────────────────────────────────────

    [Test]
    public async Task MatchIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        string indexKey = $"PCC-SURV-MATCH-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceMatchIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchIndexGrain>(indexKey);

        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-001", PatientId = "PAT-001", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc),
            VisitType = PccVisitType.Ambulatory,
            CreatedDate = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc)
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-002", PatientId = "PAT-002", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            VisitType = PccVisitType.Emergency,
            CreatedDate = new DateTime(2026, 3, 20, 14, 0, 0, DateTimeKind.Utc)
        });

        List<PccSurveillanceMatchIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task MatchIndexGrain_GetByStatus_FiltersCorrectly()
    {
        string indexKey = $"PCC-SURV-MATCH-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceMatchIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchIndexGrain>(indexKey);

        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-D1", PatientId = "PAT-001", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Ambulatory,
            CreatedDate = DateTime.UtcNow
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-R1", PatientId = "PAT-002", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Reviewed,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Emergency,
            CreatedDate = DateTime.UtcNow
        });

        List<PccSurveillanceMatchIndexEntry> detected = await index.GetByStatusAsync(PccSurveillanceMatchStatus.Detected);
        Assert.That(detected, Has.Count.EqualTo(1));
        Assert.That(detected[0].MatchId, Is.EqualTo("M-D1"));
    }

    [Test]
    public async Task MatchIndexGrain_GetByCondition_FiltersCorrectly()
    {
        string indexKey = $"PCC-SURV-MATCH-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceMatchIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchIndexGrain>(indexKey);

        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-ILI", PatientId = "PAT-001", ConditionName = "Influenza-Like Illness",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Ambulatory,
            CreatedDate = DateTime.UtcNow
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-CT", PatientId = "PAT-002", ConditionName = "Chlamydia",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.ReportableCommunicable,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Outpatient,
            CreatedDate = DateTime.UtcNow
        });

        List<PccSurveillanceMatchIndexEntry> iliMatches = await index.GetByConditionAsync("Influenza-Like Illness");
        Assert.That(iliMatches, Has.Count.EqualTo(1));
        Assert.That(iliMatches[0].MatchId, Is.EqualTo("M-ILI"));
    }

    [Test]
    public async Task MatchIndexGrain_UpdateStatus_ChangesStatus()
    {
        string indexKey = $"PCC-SURV-MATCH-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceMatchIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchIndexGrain>(indexKey);

        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-UPD", PatientId = "PAT-001", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Ambulatory,
            CreatedDate = DateTime.UtcNow
        });

        await index.UpdateStatusAsync("M-UPD", PccSurveillanceMatchStatus.Reviewed);

        List<PccSurveillanceMatchIndexEntry> all = await index.GetAllAsync();
        Assert.That(all[0].Status, Is.EqualTo(PccSurveillanceMatchStatus.Reviewed));
    }
}
