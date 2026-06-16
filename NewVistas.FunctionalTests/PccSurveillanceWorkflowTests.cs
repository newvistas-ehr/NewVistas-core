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
/// Functional tests for PCC Surveillance workflows — configuration lifecycle,
/// match creation with full encounter context, status transitions, and export.
/// Tests RPMS APCSB.m / APCSSIL2.m encounter-level surveillance patterns.
/// </summary>
[TestFixture]
public class PccSurveillanceWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ISiteParametersGrain GetSiteParams()
        => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp()
    {
        await GetSiteParams().EnableFeatureAsync("PCC_SURVEILLANCE");
    }

    // ─── Configuration Workflows ──────────────────────────────────────────────

    [Test]
    public async Task CreateConfig_ILI_AppearsInIndex()
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
            true, true, 90, new List<string> { "US", "IHS" }, "24 hours", true);

        string indexKey = $"PCC-SURV-CONFIG-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceConfigIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigIndexGrain>(indexKey);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = configId,
            ConditionName = "Influenza-Like Illness",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 3,
            IsActive = true
        });

        List<PccSurveillanceConfigIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ConditionName, Is.EqualTo("Influenza-Like Illness"));
        Assert.That(all[0].CriteriaCount, Is.EqualTo(3));
        Assert.That(all[0].IsActive, Is.True);
    }

    [Test]
    public async Task CreateConfig_SRD_HospitalizedOnly()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "Severe Respiratory Disease",
            PccEncounterClassification.SevereRespiratoryDisease,
            new List<PccSurveillanceCriterion>
            {
                new() { Code = "J80", CodeSystem = "ICD-10", Description = "ARDS", MatchType = "diagnosis" },
                new() { Code = "J96.0", CodeSystem = "ICD-10", Description = "Acute respiratory failure", MatchType = "diagnosis" },
            },
            new List<PccVisitType> { PccVisitType.Hospitalization },
            true, true, 90, new List<string> { "US" }, "Immediately", true);

        PccSurveillanceConfigState result = await grain.GetAsync();
        Assert.That(result.ConditionName, Is.EqualTo("Severe Respiratory Disease"));
        Assert.That(result.Classification, Is.EqualTo(PccEncounterClassification.SevereRespiratoryDisease));
        Assert.That(result.RequiredVisitTypes, Has.Count.EqualTo(1));
        Assert.That(result.RequiredVisitTypes, Contains.Item(PccVisitType.Hospitalization));
        Assert.That(result.ReportingTimeframe, Is.EqualTo("Immediately"));
    }

    [Test]
    public async Task AddCriterion_ExtendsConfig()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "ILI Extended", PccEncounterClassification.InfluenzaLikeIllness,
            new List<PccSurveillanceCriterion>
            {
                new() { Code = "J11.1", CodeSystem = "ICD-10", Description = "Influenza", MatchType = "diagnosis" },
            },
            null, true, true, 90, null, "24 hours", true);

        await grain.AddCriterionAsync(new PccSurveillanceCriterion
        {
            Code = "J06.9", CodeSystem = "ICD-10", Description = "Acute URI", MatchType = "diagnosis"
        });
        await grain.AddCriterionAsync(new PccSurveillanceCriterion
        {
            Code = "R50.9", CodeSystem = "ICD-10", Description = "Fever", MatchType = "diagnosis"
        });

        PccSurveillanceConfigState result = await grain.GetAsync();
        Assert.That(result.Criteria, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ToggleConfig_ActiveInactive_SyncsIndex()
    {
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await grain.SaveAsync(
            "Toggle Test", PccEncounterClassification.InfluenzaLikeIllness,
            null, null, true, true, 90, null, "24 hours", true);

        string indexKey = $"PCC-SURV-CONFIG-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceConfigIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigIndexGrain>(indexKey);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = configId, ConditionName = "Toggle Test",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 0, IsActive = true
        });

        // Deactivate
        await grain.SetActiveAsync(false);
        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = configId, ConditionName = "Toggle Test",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 0, IsActive = false
        });

        List<PccSurveillanceConfigIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active.Any(e => e.ConfigId == configId), Is.False);

        PccSurveillanceConfigState deactivated = await grain.GetAsync();
        Assert.That(deactivated.IsActive, Is.False);

        // Reactivate
        await grain.SetActiveAsync(true);
        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = configId, ConditionName = "Toggle Test",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 0, IsActive = true
        });

        active = await index.GetActiveAsync();
        Assert.That(active.Any(e => e.ConfigId == configId), Is.True);
    }

    // ─── Match Workflows ──────────────────────────────────────────────────────

    [Test]
    public async Task CreateMatch_ILI_FullEncounterContext()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        DateTime encounterDate = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(
            patientId: "PAT-ILI-001",
            patientName: "DOE,JANE",
            configId: "CFG-ILI-001",
            conditionName: "Influenza-Like Illness",
            classification: PccEncounterClassification.InfluenzaLikeIllness,
            encounterDate: encounterDate,
            visitType: PccVisitType.Ambulatory,
            chiefComplaint: "Fever, body aches, cough x 3 days",
            facilityName: "IHS Clinic Alpha",
            dischargeDate: null,
            providerName: "DR. WHITEHORSE",
            matchingDiagnoses: new List<string> { "J11.1", "R50.9" },
            matchingProcedures: null,
            matchingLabResults: new List<string> { "LOINC:33535-6" },
            matchingMedications: new List<string> { "Oseltamivir" },
            comorbidities: new PccComorbidityFlags
            {
                Asthma = true, Diabetes = true, Obesity = false,
                Pregnancy = false, Immunocompromised = false,
                ChronicLungDisease = false, CardiovascularDisease = false,
                Bmi = 28.5m
            },
            vitals: new PccEncounterVitals
            {
                TemperatureF = 102.1m, OxygenSaturationPct = 94,
                HeartRate = 105, RespiratoryRate = 22,
                BloodPressureSystolic = 128, BloodPressureDiastolic = 82
            });

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.PatientId, Is.EqualTo("PAT-ILI-001"));
        Assert.That(result.ConditionName, Is.EqualTo("Influenza-Like Illness"));
        Assert.That(result.Classification, Is.EqualTo(PccEncounterClassification.InfluenzaLikeIllness));
        Assert.That(result.VisitType, Is.EqualTo(PccVisitType.Ambulatory));
        Assert.That(result.ChiefComplaint, Is.EqualTo("Fever, body aches, cough x 3 days"));
        Assert.That(result.MatchingDiagnoses, Has.Count.EqualTo(2));
        Assert.That(result.MatchingLabResults, Has.Count.EqualTo(1));
        Assert.That(result.MatchingMedications, Has.Count.EqualTo(1));
        Assert.That(result.Comorbidities, Is.Not.Null);
        Assert.That(result.Comorbidities!.Asthma, Is.True);
        Assert.That(result.Vitals, Is.Not.Null);
        Assert.That(result.Vitals!.TemperatureF, Is.EqualTo(102.1m));
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Detected));
    }

    [Test]
    public async Task CreateMatch_SRD_Hospitalized()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        DateTime encounterDate = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime dischargeDate = new DateTime(2026, 3, 22, 14, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(
            patientId: "PAT-SRD-001",
            patientName: "REDHAWK,MICHAEL",
            configId: "CFG-SRD-001",
            conditionName: "Severe Respiratory Disease",
            classification: PccEncounterClassification.SevereRespiratoryDisease,
            encounterDate: encounterDate,
            visitType: PccVisitType.Hospitalization,
            chiefComplaint: "Severe dyspnea, hypoxia",
            facilityName: "IHS Regional Medical Center",
            dischargeDate: dischargeDate,
            providerName: "DR. WILLIAMS",
            matchingDiagnoses: new List<string> { "J80", "J96.0" },
            matchingProcedures: null,
            matchingLabResults: null,
            matchingMedications: null,
            comorbidities: null,
            vitals: new PccEncounterVitals
            {
                TemperatureF = 103.2m, OxygenSaturationPct = 88,
                RespiratoryRate = 32
            });

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.ConditionName, Is.EqualTo("Severe Respiratory Disease"));
        Assert.That(result.VisitType, Is.EqualTo(PccVisitType.Hospitalization));
        Assert.That(result.DischargeDate, Is.EqualTo(dischargeDate));
        Assert.That(result.Classification, Is.EqualTo(PccEncounterClassification.SevereRespiratoryDisease));
    }

    [Test]
    public async Task MatchStatusLifecycle_DetectedToExported()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-LC-001", "JOHNSON,ROBERT", "CFG-ILI-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null, null, null);

        // Detected (default)
        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Detected));

        // Reviewed
        await grain.UpdateStatusAsync(PccSurveillanceMatchStatus.Reviewed);
        result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Reviewed));

        // Reported
        await grain.UpdateStatusAsync(PccSurveillanceMatchStatus.Reported);
        result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Reported));

        // Exported
        await grain.MarkExportedAsync("EPILABHL7_SITE01_20260322.txt");
        result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Exported));
        Assert.That(result.ExportReference, Is.EqualTo("EPILABHL7_SITE01_20260322.txt"));
        Assert.That(result.ExportedDate, Is.Not.Null);
    }

    [Test]
    public async Task MatchWithComorbidities_AllFlagsDetected()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-CMB-001", null, "CFG-ILI-001", "ILI",
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
                Bmi = 42.0m
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
        Assert.That(result.Comorbidities.Bmi, Is.EqualTo(42.0m));
    }

    [Test]
    public async Task MatchWithVitals_TemperatureAndO2()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-VIT-001", null, "CFG-ILI-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Emergency,
            null, null, null, null, null, null, null, null,
            comorbidities: null,
            vitals: new PccEncounterVitals
            {
                TemperatureF = 102.1m,
                OxygenSaturationPct = 91,
                RespiratoryRate = 24
            });

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Vitals, Is.Not.Null);
        Assert.That(result.Vitals!.TemperatureF, Is.EqualTo(102.1m));
        Assert.That(result.Vitals.OxygenSaturationPct, Is.EqualTo(91));
        Assert.That(result.Vitals.RespiratoryRate, Is.EqualTo(24));
    }

    [Test]
    public async Task FilterMatchesByStatus_ReturnsCorrectSubset()
    {
        string indexKey = $"PCC-SURV-MATCH-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceMatchIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchIndexGrain>(indexKey);

        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-DET", PatientId = "PAT-001", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Ambulatory,
            CreatedDate = DateTime.UtcNow
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-REV", PatientId = "PAT-002", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Reviewed,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Emergency,
            CreatedDate = DateTime.UtcNow
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-EXP", PatientId = "PAT-003", ConditionName = "ILI",
            Status = PccSurveillanceMatchStatus.Exported,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Ambulatory,
            CreatedDate = DateTime.UtcNow
        });

        List<PccSurveillanceMatchIndexEntry> detected = await index.GetByStatusAsync(PccSurveillanceMatchStatus.Detected);
        Assert.That(detected, Has.Count.EqualTo(1));
        Assert.That(detected[0].MatchId, Is.EqualTo("M-DET"));

        List<PccSurveillanceMatchIndexEntry> reviewed = await index.GetByStatusAsync(PccSurveillanceMatchStatus.Reviewed);
        Assert.That(reviewed, Has.Count.EqualTo(1));
        Assert.That(reviewed[0].MatchId, Is.EqualTo("M-REV"));

        List<PccSurveillanceMatchIndexEntry> exported = await index.GetByStatusAsync(PccSurveillanceMatchStatus.Exported);
        Assert.That(exported, Has.Count.EqualTo(1));
        Assert.That(exported[0].MatchId, Is.EqualTo("M-EXP"));
    }

    [Test]
    public async Task FilterMatchesByCondition_ReturnsCorrectSubset()
    {
        string indexKey = $"PCC-SURV-MATCH-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceMatchIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchIndexGrain>(indexKey);

        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-ILI-1", PatientId = "PAT-001", ConditionName = "Influenza-Like Illness",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Ambulatory,
            CreatedDate = DateTime.UtcNow
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-ILI-2", PatientId = "PAT-002", ConditionName = "Influenza-Like Illness",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Emergency,
            CreatedDate = DateTime.UtcNow
        });
        await index.AddEntryAsync(new PccSurveillanceMatchIndexEntry
        {
            MatchId = "M-CT-1", PatientId = "PAT-003", ConditionName = "Chlamydia",
            Status = PccSurveillanceMatchStatus.Detected,
            Classification = PccEncounterClassification.ReportableCommunicable,
            EncounterDate = DateTime.UtcNow, VisitType = PccVisitType.Outpatient,
            CreatedDate = DateTime.UtcNow
        });

        List<PccSurveillanceMatchIndexEntry> iliMatches = await index.GetByConditionAsync("Influenza-Like Illness");
        Assert.That(iliMatches, Has.Count.EqualTo(2));

        List<PccSurveillanceMatchIndexEntry> ctMatches = await index.GetByConditionAsync("Chlamydia");
        Assert.That(ctMatches, Has.Count.EqualTo(1));
        Assert.That(ctMatches[0].MatchId, Is.EqualTo("M-CT-1"));
    }

    [Test]
    public async Task MultipleConfigs_IndependentConditions()
    {
        string indexKey = $"PCC-SURV-CONFIG-IDX-{Guid.NewGuid():N}";
        IPccSurveillanceConfigIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigIndexGrain>(indexKey);

        // Create ILI config
        string iliConfigId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain iliGrain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{iliConfigId}");

        await iliGrain.SaveAsync(
            "Influenza-Like Illness", PccEncounterClassification.InfluenzaLikeIllness,
            new List<PccSurveillanceCriterion>
            {
                new() { Code = "J11.1", CodeSystem = "ICD-10", Description = "Influenza", MatchType = "diagnosis" },
            },
            null, true, true, 90, null, "24 hours", true);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = iliConfigId, ConditionName = "Influenza-Like Illness",
            Classification = PccEncounterClassification.InfluenzaLikeIllness,
            CriteriaCount = 1, IsActive = true
        });

        // Create Chlamydia config
        string ctConfigId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain ctGrain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{ctConfigId}");

        await ctGrain.SaveAsync(
            "Chlamydia", PccEncounterClassification.ReportableCommunicable,
            new List<PccSurveillanceCriterion>
            {
                new() { Code = "A56.0", CodeSystem = "ICD-10", Description = "Chlamydial infection of lower genitourinary tract", MatchType = "diagnosis" },
            },
            null, true, false, 30, null, "5 days", true);

        await index.UpsertAsync(new PccSurveillanceConfigIndexEntry
        {
            ConfigId = ctConfigId, ConditionName = "Chlamydia",
            Classification = PccEncounterClassification.ReportableCommunicable,
            CriteriaCount = 1, IsActive = true
        });

        // Verify independent
        List<PccSurveillanceConfigIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Any(e => e.ConditionName == "Influenza-Like Illness"), Is.True);
        Assert.That(all.Any(e => e.ConditionName == "Chlamydia"), Is.True);

        PccSurveillanceConfigState iliResult = await iliGrain.GetAsync();
        PccSurveillanceConfigState ctResult = await ctGrain.GetAsync();
        Assert.That(iliResult.ConditionName, Is.EqualTo("Influenza-Like Illness"));
        Assert.That(ctResult.ConditionName, Is.EqualTo("Chlamydia"));
    }

    [Test]
    public async Task FullWorkflow_ConfigToExport()
    {
        // 1. Create config
        string configId = Guid.NewGuid().ToString("N");
        IPccSurveillanceConfigGrain configGrain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

        await configGrain.SaveAsync(
            "ILI Full Workflow", PccEncounterClassification.InfluenzaLikeIllness,
            new List<PccSurveillanceCriterion>
            {
                new() { Code = "J11.1", CodeSystem = "ICD-10", Description = "Influenza", MatchType = "diagnosis" },
                new() { Code = "R50.9", CodeSystem = "ICD-10", Description = "Fever", MatchType = "diagnosis" },
            },
            new List<PccVisitType> { PccVisitType.Ambulatory },
            true, true, 90, new List<string> { "US" }, "24 hours", true);

        PccSurveillanceConfigState config = await configGrain.GetAsync();
        Assert.That(config.IsActive, Is.True);
        Assert.That(config.Criteria, Has.Count.EqualTo(2));

        // 2. Create match
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain matchGrain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        DateTime encounterDate = new DateTime(2026, 3, 22, 9, 0, 0, DateTimeKind.Utc);

        await matchGrain.CreateAsync(
            "PAT-FW-001", "FULLWORKFLOW,PATIENT", configId, "ILI Full Workflow",
            PccEncounterClassification.InfluenzaLikeIllness,
            encounterDate, PccVisitType.Ambulatory,
            "Fever and cough", "IHS Clinic", null, "DR. SMITH",
            new List<string> { "J11.1" }, null,
            new List<string> { "LOINC:33535-6" }, null,
            new PccComorbidityFlags { Asthma = true },
            new PccEncounterVitals { TemperatureF = 101.0m, OxygenSaturationPct = 96 });

        PccSurveillanceMatchState match = await matchGrain.GetAsync();
        Assert.That(match.Status, Is.EqualTo(PccSurveillanceMatchStatus.Detected));

        // 3. Update status through lifecycle
        await matchGrain.UpdateStatusAsync(PccSurveillanceMatchStatus.Reviewed);
        match = await matchGrain.GetAsync();
        Assert.That(match.Status, Is.EqualTo(PccSurveillanceMatchStatus.Reviewed));

        await matchGrain.UpdateStatusAsync(PccSurveillanceMatchStatus.Reported);
        match = await matchGrain.GetAsync();
        Assert.That(match.Status, Is.EqualTo(PccSurveillanceMatchStatus.Reported));

        // 4. Export
        await matchGrain.MarkExportedAsync("EPILABHL7_SITE01_20260322.txt");
        match = await matchGrain.GetAsync();
        Assert.That(match.Status, Is.EqualTo(PccSurveillanceMatchStatus.Exported));
        Assert.That(match.ExportReference, Is.EqualTo("EPILABHL7_SITE01_20260322.txt"));
        Assert.That(match.ExportedDate, Is.Not.Null);

        // 5. Verify all fields still intact
        Assert.That(match.PatientId, Is.EqualTo("PAT-FW-001"));
        Assert.That(match.ConditionName, Is.EqualTo("ILI Full Workflow"));
        Assert.That(match.EncounterDate, Is.EqualTo(encounterDate));
        Assert.That(match.Comorbidities!.Asthma, Is.True);
        Assert.That(match.Vitals!.TemperatureF, Is.EqualTo(101.0m));
    }

    [Test]
    public async Task ExportMatch_SetsDateAndReference()
    {
        string matchId = Guid.NewGuid().ToString("N");
        IPccSurveillanceMatchGrain grain = _cluster.GrainFactory
            .GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

        await grain.CreateAsync(
            "PAT-EXP-001", null, "CFG-001", "ILI",
            PccEncounterClassification.InfluenzaLikeIllness,
            DateTime.UtcNow, PccVisitType.Ambulatory,
            null, null, null, null, null, null, null, null, null, null);

        DateTime beforeExport = DateTime.UtcNow;
        await grain.MarkExportedAsync("EPILABHL7_SITE01_20260322.txt");

        PccSurveillanceMatchState result = await grain.GetAsync();
        Assert.That(result.Status, Is.EqualTo(PccSurveillanceMatchStatus.Exported));
        Assert.That(result.ExportReference, Is.EqualTo("EPILABHL7_SITE01_20260322.txt"));
        Assert.That(result.ExportedDate, Is.Not.Null);
        Assert.That(result.ExportedDate!.Value, Is.GreaterThanOrEqualTo(beforeExport));
    }
}
