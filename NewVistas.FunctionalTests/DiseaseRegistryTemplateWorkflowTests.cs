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
/// Functional tests for enhanced Clinical Case Registries — diabetes enrichment and asthma registry.
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end enrollment, enriched data updates, and cross-grain coordination.
/// </summary>
[TestFixture]
public class DiseaseRegistryTemplateWorkflowTests
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

    private static async Task EnrollPatient(
        IClinicalRegistryEntryGrain grain,
        string patientId,
        RegistryType type,
        string patientName = "Test Patient")
    {
        await grain.EnrollPatientAsync(
            patientId, patientName, new DateTime(1965, 6, 15),
            type, "PRV-001", "Dr. Enrolling",
            "SITE-001", "VA Medical Center",
            "PRV-002", "Dr. Primary", null);
    }

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DiabetesEnrollAndEnrich_FullWorkflow()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);

        // Enroll
        await EnrollPatient(grain, patientId, RegistryType.DiabetesMellitus, "DM Full Workflow");

        // Update basic diabetes data
        await grain.UpdateDiabetesDataAsync(
            DiabetesType.Type2, 8.2m, DateTime.UtcNow.AddDays(-14),
            true, new List<string> { "Retinopathy", "Nephropathy" });

        // Update enriched data
        DateTime ldlDate = DateTime.UtcNow.AddDays(-10);
        DateTime microDate = DateTime.UtcNow.AddDays(-10);
        DateTime bpDate = DateTime.UtcNow.AddDays(-1);

        await grain.UpdateDiabetesEnrichedDataAsync(
            92m, ldlDate,
            18.5m, microDate,
            135, 88, bpDate,
            new DiabetesMedicationStatus
            {
                Insulin = true,
                Metformin = true,
                Statin = true,
                SGLT2Inhibitor = true,
                ACEInhibitorOrARB = true
            },
            new DiabetesExamRecord
            {
                FootExamDone = true, FootExamDate = DateTime.UtcNow.AddDays(-30),
                EyeExamDone = true, EyeExamDate = DateTime.UtcNow.AddDays(-60),
                DentalExamDone = true, DentalExamDate = DateTime.UtcNow.AddDays(-90)
            },
            new DiabetesEducationRecord
            {
                DietInstruction = true,
                ExerciseInstruction = true,
                SelfMonitoringEducation = true
            });

        // Verify all fields
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.DiabetesMellitus));
        Assert.That(state.DiabetesType, Is.EqualTo(DiabetesType.Type2));
        Assert.That(state.HbA1cPct, Is.EqualTo(8.2m));
        Assert.That(state.IsInsulinDependent, Is.True);
        Assert.That(state.DiabetesComplications, Has.Count.EqualTo(2));
        Assert.That(state.LdlMgDl, Is.EqualTo(92m));
        Assert.That(state.MicroalbuminMgL, Is.EqualTo(18.5m));
        Assert.That(state.BloodPressureSystolic, Is.EqualTo(135));
        Assert.That(state.BloodPressureDiastolic, Is.EqualTo(88));
        Assert.That(state.DiabetesMedications!.Insulin, Is.True);
        Assert.That(state.DiabetesMedications.SGLT2Inhibitor, Is.True);
        Assert.That(state.DiabetesExams!.FootExamDone, Is.True);
        Assert.That(state.DiabetesExams.EyeExamDone, Is.True);
        Assert.That(state.DiabetesEducation!.DietInstruction, Is.True);
        Assert.That(state.DiabetesEducation.SelfMonitoringEducation, Is.True);
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DiabetesEnriched_MedicationCategories_PersistCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);
        await EnrollPatient(grain, patientId, RegistryType.DiabetesMellitus);

        DiabetesMedicationStatus meds = new()
        {
            Insulin = true,
            Metformin = true,
            Sulfonylurea = false,
            Glitazone = false,
            DPP4Inhibitor = true,
            GLP1Agonist = true,
            SGLT2Inhibitor = true,
            Acarbose = false,
            Statin = true,
            ACEInhibitorOrARB = false,
            Aspirin = false,
            DietControlOnly = false
        };

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null, null, null, null,
            meds, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesMedications, Is.Not.Null);
        Assert.That(state.DiabetesMedications!.Insulin, Is.True);
        Assert.That(state.DiabetesMedications.Metformin, Is.True);
        Assert.That(state.DiabetesMedications.Sulfonylurea, Is.False);
        Assert.That(state.DiabetesMedications.DPP4Inhibitor, Is.True);
        Assert.That(state.DiabetesMedications.GLP1Agonist, Is.True);
        Assert.That(state.DiabetesMedications.SGLT2Inhibitor, Is.True);
        Assert.That(state.DiabetesMedications.Statin, Is.True);
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DiabetesEnriched_ExamTracking_PersistsWithDates()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);
        await EnrollPatient(grain, patientId, RegistryType.DiabetesMellitus);

        DateTime footDate = DateTime.UtcNow.AddDays(-15);
        DateTime eyeDate = DateTime.UtcNow.AddDays(-45);
        DateTime dentalDate = DateTime.UtcNow.AddDays(-90);

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null, null, null, null,
            null,
            new DiabetesExamRecord
            {
                FootExamDone = true, FootExamDate = footDate,
                EyeExamDone = true, EyeExamDate = eyeDate,
                DentalExamDone = true, DentalExamDate = dentalDate
            },
            null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesExams, Is.Not.Null);
        Assert.That(state.DiabetesExams!.FootExamDone, Is.True);
        Assert.That(state.DiabetesExams.FootExamDate, Is.EqualTo(footDate));
        Assert.That(state.DiabetesExams.EyeExamDone, Is.True);
        Assert.That(state.DiabetesExams.EyeExamDate, Is.EqualTo(eyeDate));
        Assert.That(state.DiabetesExams.DentalExamDone, Is.True);
        Assert.That(state.DiabetesExams.DentalExamDate, Is.EqualTo(dentalDate));
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DiabetesEnriched_EducationTracking_PersistsAllFlags()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);
        await EnrollPatient(grain, patientId, RegistryType.DiabetesMellitus);

        DateTime educationDate = DateTime.UtcNow.AddDays(-7);

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null, null, null, null,
            null, null,
            new DiabetesEducationRecord
            {
                DietInstruction = true,
                ExerciseInstruction = true,
                OtherDMEducation = true,
                TobaccoCessationCounseling = true,
                SelfMonitoringEducation = true,
                LastEducationDate = educationDate
            });

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesEducation, Is.Not.Null);
        Assert.That(state.DiabetesEducation!.DietInstruction, Is.True);
        Assert.That(state.DiabetesEducation.ExerciseInstruction, Is.True);
        Assert.That(state.DiabetesEducation.OtherDMEducation, Is.True);
        Assert.That(state.DiabetesEducation.TobaccoCessationCounseling, Is.True);
        Assert.That(state.DiabetesEducation.SelfMonitoringEducation, Is.True);
        Assert.That(state.DiabetesEducation.LastEducationDate, Is.EqualTo(educationDate));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DiabetesEnriched_LabValues_IndependentOfBasicDiabetes()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);
        await EnrollPatient(grain, patientId, RegistryType.DiabetesMellitus);

        // Set basic diabetes data
        await grain.UpdateDiabetesDataAsync(
            DiabetesType.Type1, 7.2m, DateTime.UtcNow.AddDays(-30),
            true, new List<string> { "Neuropathy" });

        // Set enriched lab data
        await grain.UpdateDiabetesEnrichedDataAsync(
            105m, DateTime.UtcNow.AddDays(-7),
            22.3m, DateTime.UtcNow.AddDays(-7),
            125, 80, DateTime.UtcNow.AddDays(-1),
            null, null, null);

        // Verify both sets are intact
        ClinicalRegistryEntryState state = await grain.GetEntryAsync();

        // Basic fields
        Assert.That(state.DiabetesType, Is.EqualTo(DiabetesType.Type1));
        Assert.That(state.HbA1cPct, Is.EqualTo(7.2m));
        Assert.That(state.IsInsulinDependent, Is.True);
        Assert.That(state.DiabetesComplications, Has.Count.EqualTo(1));
        Assert.That(state.DiabetesComplications, Contains.Item("Neuropathy"));

        // Enriched fields
        Assert.That(state.LdlMgDl, Is.EqualTo(105m));
        Assert.That(state.MicroalbuminMgL, Is.EqualTo(22.3m));
        Assert.That(state.BloodPressureSystolic, Is.EqualTo(125));
        Assert.That(state.BloodPressureDiastolic, Is.EqualTo(80));
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task AsthmaEnroll_AndUpdateData_FullWorkflow()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(grain, patientId, RegistryType.Asthma, "Asthma Workflow Patient");

        DateTime diagnosisDate = DateTime.UtcNow.AddYears(-8);
        DateTime spirometryDate = DateTime.UtcNow.AddDays(-14);
        List<string> triggers = new() { "dust mites", "exercise", "cold air", "tobacco smoke" };

        await grain.UpdateAsthmaDataAsync(
            diagnosisDate,
            AsthmaSeverity.ModeratePersistent,
            AsthmaControlLevel.NotWellControlled,
            spirometryDate,
            68m, 0.65m,
            320, 420,
            "Fluticasone/Salmeterol",
            "Albuterol",
            true,
            triggers,
            3);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.Asthma));
        Assert.That(state.PatientName, Is.EqualTo("Asthma Workflow Patient"));
        Assert.That(state.AsthmaDiagnosisDate, Is.EqualTo(diagnosisDate));
        Assert.That(state.AsthmaSeverity, Is.EqualTo(AsthmaSeverity.ModeratePersistent));
        Assert.That(state.AsthmaControlLevel, Is.EqualTo(AsthmaControlLevel.NotWellControlled));
        Assert.That(state.Fev1PredictedPct, Is.EqualTo(68m));
        Assert.That(state.Fev1FvcRatio, Is.EqualTo(0.65m));
        Assert.That(state.PeakFlowLPerMin, Is.EqualTo(320));
        Assert.That(state.PeakFlowPersonalBest, Is.EqualTo(420));
        Assert.That(state.ControllerMedication, Is.EqualTo("Fluticasone/Salmeterol"));
        Assert.That(state.RescueMedication, Is.EqualTo("Albuterol"));
        Assert.That(state.HasAsthmaActionPlan, Is.True);
        Assert.That(state.AsthmaTriggers, Has.Count.EqualTo(4));
        Assert.That(state.AsthmaEdVisitsLast12Months, Is.EqualTo(3));
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task AsthmaEnroll_SeverityAndControl_Tracked()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(grain, patientId, RegistryType.Asthma);

        await grain.UpdateAsthmaDataAsync(
            null,
            AsthmaSeverity.SeverePersistent,
            AsthmaControlLevel.VeryPoorlyControlled,
            null, null, null, null, null,
            null, null, false, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.AsthmaSeverity, Is.EqualTo(AsthmaSeverity.SeverePersistent));
        Assert.That(state.AsthmaControlLevel, Is.EqualTo(AsthmaControlLevel.VeryPoorlyControlled));
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task AsthmaEnroll_Spirometry_PFTValues()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(grain, patientId, RegistryType.Asthma);

        DateTime spirometryDate = DateTime.UtcNow.AddDays(-5);

        await grain.UpdateAsthmaDataAsync(
            null, null, null,
            spirometryDate, 75m, 0.70m,
            null, null, null, null, false, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.SpirometryDate, Is.EqualTo(spirometryDate));
        Assert.That(state.Fev1PredictedPct, Is.EqualTo(75m));
        Assert.That(state.Fev1FvcRatio, Is.EqualTo(0.70m));
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task AsthmaEnroll_MedsAndActionPlan()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(grain, patientId, RegistryType.Asthma);

        await grain.UpdateAsthmaDataAsync(
            null, null, null, null, null, null, null, null,
            "Mometasone/Formoterol", "Albuterol HFA",
            true, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.ControllerMedication, Is.EqualTo("Mometasone/Formoterol"));
        Assert.That(state.RescueMedication, Is.EqualTo("Albuterol HFA"));
        Assert.That(state.HasAsthmaActionPlan, Is.True);
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task AsthmaEnroll_Triggers_ManagedCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(grain, patientId, RegistryType.Asthma);

        List<string> triggers = new() { "pollen", "mold", "pet dander", "dust mites", "cold air" };

        await grain.UpdateAsthmaDataAsync(
            null, null, null, null, null, null, null, null,
            null, null, false, triggers, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.AsthmaTriggers, Has.Count.EqualTo(5));
        Assert.That(state.AsthmaTriggers, Contains.Item("pollen"));
        Assert.That(state.AsthmaTriggers, Contains.Item("mold"));
        Assert.That(state.AsthmaTriggers, Contains.Item("pet dander"));
        Assert.That(state.AsthmaTriggers, Contains.Item("dust mites"));
        Assert.That(state.AsthmaTriggers, Contains.Item("cold air"));
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task AsthmaEnroll_AppearsInIndex()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IClinicalRegistryEntryGrain grain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(grain, patientId, RegistryType.Asthma, "Indexed Asthma Patient");

        // Add to registry index
        IClinicalRegistryIndexGrain index = GetRegistryIndex(RegistryType.Asthma);
        await index.UpsertEntryAsync(new CCREntrySummary
        {
            PatientId = patientId,
            PatientName = "Indexed Asthma Patient",
            RegistryType = RegistryType.Asthma,
            Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            SiteId = "SITE-001",
            PrimaryProviderName = "Dr. Primary",
            LastModifiedDate = DateTime.UtcNow
        });

        List<CCREntrySummary> entries = await index.GetAllEntriesAsync();
        Assert.That(entries.Any(e => e.PatientId == patientId), Is.True);
        Assert.That(entries.First(e => e.PatientId == patientId).RegistryType, Is.EqualTo(RegistryType.Asthma));
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task MultiDisease_PatientInBothRegistries()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";

        // Enroll in Diabetes
        IClinicalRegistryEntryGrain dmGrain = GetEntryGrain(RegistryType.DiabetesMellitus, patientId);
        await EnrollPatient(dmGrain, patientId, RegistryType.DiabetesMellitus, "Multi-Disease Patient");

        // Enroll in Asthma
        IClinicalRegistryEntryGrain asthmaGrain = GetEntryGrain(RegistryType.Asthma, patientId);
        await EnrollPatient(asthmaGrain, patientId, RegistryType.Asthma, "Multi-Disease Patient");

        // Update patient registry list
        IPatientRegistryListGrain patList = GetPatientList(patientId);
        await patList.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.DiabetesMellitus,
            Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            PrimaryProviderName = "Dr. Endocrine"
        });
        await patList.UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
        {
            RegistryType = RegistryType.Asthma,
            Status = CCREnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            PrimaryProviderName = "Dr. Pulmonary"
        });

        // Verify CCR-PAT grain shows both
        List<PatientRegistryEnrollmentEntry> enrollments = await patList.GetAllEnrollmentsAsync();
        Assert.That(enrollments, Has.Count.EqualTo(2));
        Assert.That(enrollments.Any(e => e.RegistryType == RegistryType.DiabetesMellitus), Is.True);
        Assert.That(enrollments.Any(e => e.RegistryType == RegistryType.Asthma), Is.True);

        // Verify each entry grain is independent
        ClinicalRegistryEntryState dmState = await dmGrain.GetEntryAsync();
        Assert.That(dmState.RegistryType, Is.EqualTo(RegistryType.DiabetesMellitus));

        ClinicalRegistryEntryState asthmaState = await asthmaGrain.GetEntryAsync();
        Assert.That(asthmaState.RegistryType, Is.EqualTo(RegistryType.Asthma));
    }
}
