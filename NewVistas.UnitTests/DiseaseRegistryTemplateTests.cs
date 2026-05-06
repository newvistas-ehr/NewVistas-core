// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

[TestFixture]
public class DiseaseRegistryTemplateTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<IClinicalRegistryEntryGrain> EnrollDiabetesGrain(string patientId)
    {
        string key = $"CCR:DiabetesMellitus:{patientId}";
        IClinicalRegistryEntryGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryEntryGrain>(key);
        await grain.EnrollPatientAsync(
            patientId, "DM Patient", new DateTime(1960, 5, 15),
            RegistryType.DiabetesMellitus, "PRV-001", "Dr. Endocrine",
            "VAMC-01", "VA Medical Center 1",
            "PRV-002", "Dr. Primary", "Diabetes enrollment");
        return grain;
    }

    private async Task<IClinicalRegistryEntryGrain> EnrollAsthmaGrain(string patientId)
    {
        string key = $"CCR:Asthma:{patientId}";
        IClinicalRegistryEntryGrain grain = _cluster.GrainFactory.GetGrain<IClinicalRegistryEntryGrain>(key);
        await grain.EnrollPatientAsync(
            patientId, "Asthma Patient", new DateTime(1985, 9, 22),
            RegistryType.Asthma, "PRV-001", "Dr. Pulmonary",
            "VAMC-01", "VA Medical Center 1",
            "PRV-003", "Dr. Primary", "Asthma enrollment");
        return grain;
    }

    // ── Diabetes Enrichment Tests ─────────────────────────────────────────────

    [Test]
    public async Task DiabetesEnriched_UpdateLabs_PersistsLdlAndMicroalbumin()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        DateTime ldlDate = DateTime.UtcNow.AddDays(-7);
        DateTime microalbuminDate = DateTime.UtcNow.AddDays(-5);

        await grain.UpdateDiabetesEnrichedDataAsync(
            95.5m, ldlDate,
            15.2m, microalbuminDate,
            null, null, null,
            null, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.LdlMgDl, Is.EqualTo(95.5m));
        Assert.That(state.LdlDate, Is.EqualTo(ldlDate));
        Assert.That(state.MicroalbuminMgL, Is.EqualTo(15.2m));
        Assert.That(state.MicroalbuminDate, Is.EqualTo(microalbuminDate));
    }

    [Test]
    public async Task DiabetesEnriched_UpdateBloodPressure_PersistsBpFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        DateTime bpDate = DateTime.UtcNow.AddDays(-1);

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null,
            130, 85, bpDate,
            null, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.BloodPressureSystolic, Is.EqualTo(130));
        Assert.That(state.BloodPressureDiastolic, Is.EqualTo(85));
        Assert.That(state.BloodPressureDate, Is.EqualTo(bpDate));
    }

    [Test]
    public async Task DiabetesEnriched_UpdateMedications_PersistsAllCategories()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        DiabetesMedicationStatus meds = new()
        {
            Insulin = true,
            Metformin = true,
            Statin = true,
            SGLT2Inhibitor = true
        };

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null,
            null, null, null,
            meds, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesMedications, Is.Not.Null);
        Assert.That(state.DiabetesMedications!.Insulin, Is.True);
        Assert.That(state.DiabetesMedications.Metformin, Is.True);
        Assert.That(state.DiabetesMedications.Statin, Is.True);
        Assert.That(state.DiabetesMedications.SGLT2Inhibitor, Is.True);
    }

    [Test]
    public async Task DiabetesEnriched_UpdateExams_PersistsFootEyeDental()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        DateTime footDate = DateTime.UtcNow.AddDays(-30);
        DateTime eyeDate = DateTime.UtcNow.AddDays(-60);

        DiabetesExamRecord exams = new()
        {
            FootExamDone = true,
            FootExamDate = footDate,
            EyeExamDone = true,
            EyeExamDate = eyeDate,
            DentalExamDone = false,
            DentalExamDate = null
        };

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null,
            null, null, null,
            null, exams, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesExams, Is.Not.Null);
        Assert.That(state.DiabetesExams!.FootExamDone, Is.True);
        Assert.That(state.DiabetesExams.FootExamDate, Is.EqualTo(footDate));
        Assert.That(state.DiabetesExams.EyeExamDone, Is.True);
        Assert.That(state.DiabetesExams.EyeExamDate, Is.EqualTo(eyeDate));
        Assert.That(state.DiabetesExams.DentalExamDone, Is.False);
        Assert.That(state.DiabetesExams.DentalExamDate, Is.Null);
    }

    [Test]
    public async Task DiabetesEnriched_UpdateEducation_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        DiabetesEducationRecord education = new()
        {
            DietInstruction = true,
            ExerciseInstruction = true,
            SelfMonitoringEducation = true,
            OtherDMEducation = false,
            TobaccoCessationCounseling = false,
            LastEducationDate = DateTime.UtcNow.AddDays(-14)
        };

        await grain.UpdateDiabetesEnrichedDataAsync(
            null, null, null, null,
            null, null, null,
            null, null, education);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.DiabetesEducation, Is.Not.Null);
        Assert.That(state.DiabetesEducation!.DietInstruction, Is.True);
        Assert.That(state.DiabetesEducation.ExerciseInstruction, Is.True);
        Assert.That(state.DiabetesEducation.SelfMonitoringEducation, Is.True);
        Assert.That(state.DiabetesEducation.OtherDMEducation, Is.False);
        Assert.That(state.DiabetesEducation.TobaccoCessationCounseling, Is.False);
    }

    [Test]
    public async Task DiabetesEnriched_FullUpdate_AllFieldsTogether()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        DateTime ldlDate = DateTime.UtcNow.AddDays(-10);
        DateTime microDate = DateTime.UtcNow.AddDays(-10);
        DateTime bpDate = DateTime.UtcNow.AddDays(-1);

        DiabetesMedicationStatus meds = new()
        {
            Insulin = true,
            Metformin = true,
            Statin = true,
            SGLT2Inhibitor = true,
            ACEInhibitorOrARB = true
        };

        DiabetesExamRecord exams = new()
        {
            FootExamDone = true,
            FootExamDate = DateTime.UtcNow.AddDays(-30),
            EyeExamDone = true,
            EyeExamDate = DateTime.UtcNow.AddDays(-90),
            DentalExamDone = true,
            DentalExamDate = DateTime.UtcNow.AddDays(-120)
        };

        DiabetesEducationRecord education = new()
        {
            DietInstruction = true,
            ExerciseInstruction = true,
            SelfMonitoringEducation = true,
            OtherDMEducation = true,
            TobaccoCessationCounseling = true,
            LastEducationDate = DateTime.UtcNow.AddDays(-7)
        };

        await grain.UpdateDiabetesEnrichedDataAsync(
            95.5m, ldlDate,
            15.2m, microDate,
            130, 85, bpDate,
            meds, exams, education);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.LdlMgDl, Is.EqualTo(95.5m));
        Assert.That(state.MicroalbuminMgL, Is.EqualTo(15.2m));
        Assert.That(state.BloodPressureSystolic, Is.EqualTo(130));
        Assert.That(state.BloodPressureDiastolic, Is.EqualTo(85));
        Assert.That(state.DiabetesMedications, Is.Not.Null);
        Assert.That(state.DiabetesMedications!.Insulin, Is.True);
        Assert.That(state.DiabetesExams, Is.Not.Null);
        Assert.That(state.DiabetesExams!.FootExamDone, Is.True);
        Assert.That(state.DiabetesEducation, Is.Not.Null);
        Assert.That(state.DiabetesEducation!.DietInstruction, Is.True);
    }

    // ── Asthma Registry Tests ─────────────────────────────────────────────────

    [Test]
    public async Task AsthmaRegistry_Enroll_SetsCorrectType()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.RegistryType, Is.EqualTo(RegistryType.Asthma));
        Assert.That(state.EnrollmentStatus, Is.EqualTo(CCREnrollmentStatus.Active));
    }

    [Test]
    public async Task AsthmaRegistry_UpdateData_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        DateTime spirometryDate = DateTime.UtcNow.AddDays(-14);
        List<string> triggers = new() { "dust", "exercise", "cold air" };

        await grain.UpdateAsthmaDataAsync(
            DateTime.UtcNow.AddYears(-5),
            AsthmaSeverity.ModeratePersistent,
            AsthmaControlLevel.NotWellControlled,
            spirometryDate,
            72m,
            0.68m,
            350,
            400,
            "Fluticasone/Salmeterol",
            "Albuterol",
            true,
            triggers,
            2);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.AsthmaSeverity, Is.EqualTo(AsthmaSeverity.ModeratePersistent));
        Assert.That(state.AsthmaControlLevel, Is.EqualTo(AsthmaControlLevel.NotWellControlled));
        Assert.That(state.Fev1PredictedPct, Is.EqualTo(72m));
        Assert.That(state.Fev1FvcRatio, Is.EqualTo(0.68m));
        Assert.That(state.PeakFlowLPerMin, Is.EqualTo(350));
        Assert.That(state.PeakFlowPersonalBest, Is.EqualTo(400));
        Assert.That(state.ControllerMedication, Is.EqualTo("Fluticasone/Salmeterol"));
        Assert.That(state.RescueMedication, Is.EqualTo("Albuterol"));
        Assert.That(state.HasAsthmaActionPlan, Is.True);
        Assert.That(state.AsthmaTriggers, Has.Count.EqualTo(3));
        Assert.That(state.AsthmaTriggers, Contains.Item("dust"));
        Assert.That(state.AsthmaTriggers, Contains.Item("exercise"));
        Assert.That(state.AsthmaTriggers, Contains.Item("cold air"));
        Assert.That(state.AsthmaEdVisitsLast12Months, Is.EqualTo(2));
    }

    [Test]
    public async Task AsthmaRegistry_UpdateSeverity_Changes()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        // Set initial severity
        await grain.UpdateAsthmaDataAsync(
            null, AsthmaSeverity.Intermittent, null,
            null, null, null, null, null,
            null, null, false, null, null);

        ClinicalRegistryEntryState state1 = await grain.GetEntryAsync();
        Assert.That(state1.AsthmaSeverity, Is.EqualTo(AsthmaSeverity.Intermittent));

        // Update severity
        await grain.UpdateAsthmaDataAsync(
            null, AsthmaSeverity.SeverePersistent, null,
            null, null, null, null, null,
            null, null, false, null, null);

        ClinicalRegistryEntryState state2 = await grain.GetEntryAsync();
        Assert.That(state2.AsthmaSeverity, Is.EqualTo(AsthmaSeverity.SeverePersistent));
    }

    [Test]
    public async Task AsthmaRegistry_UpdateSpirometry_PersistsPFT()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        DateTime spirometryDate = DateTime.UtcNow.AddDays(-3);

        await grain.UpdateAsthmaDataAsync(
            null, null, null,
            spirometryDate, 78m, 0.72m,
            null, null, null, null, false, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.SpirometryDate, Is.EqualTo(spirometryDate));
        Assert.That(state.Fev1PredictedPct, Is.EqualTo(78m));
        Assert.That(state.Fev1FvcRatio, Is.EqualTo(0.72m));
    }

    [Test]
    public async Task AsthmaRegistry_UpdatePeakFlow_PersistsBothValues()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        await grain.UpdateAsthmaDataAsync(
            null, null, null,
            null, null, null,
            380, 450,
            null, null, false, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.PeakFlowLPerMin, Is.EqualTo(380));
        Assert.That(state.PeakFlowPersonalBest, Is.EqualTo(450));
    }

    [Test]
    public async Task AsthmaRegistry_UpdateMedications_PersistsControllerAndRescue()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        await grain.UpdateAsthmaDataAsync(
            null, null, null,
            null, null, null,
            null, null,
            "Budesonide/Formoterol", "Levalbuterol",
            false, null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.ControllerMedication, Is.EqualTo("Budesonide/Formoterol"));
        Assert.That(state.RescueMedication, Is.EqualTo("Levalbuterol"));
    }

    [Test]
    public async Task AsthmaRegistry_ActionPlan_ToggleOnOff()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        // Set action plan to true
        await grain.UpdateAsthmaDataAsync(
            null, null, null, null, null, null,
            null, null, null, null,
            true, null, null);

        ClinicalRegistryEntryState state1 = await grain.GetEntryAsync();
        Assert.That(state1.HasAsthmaActionPlan, Is.True);

        // Set action plan to false
        await grain.UpdateAsthmaDataAsync(
            null, null, null, null, null, null,
            null, null, null, null,
            false, null, null);

        ClinicalRegistryEntryState state2 = await grain.GetEntryAsync();
        Assert.That(state2.HasAsthmaActionPlan, Is.False);
    }

    [Test]
    public async Task AsthmaRegistry_Triggers_PersistsList()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        List<string> triggers = new() { "dust", "exercise", "cold air", "pet dander" };

        await grain.UpdateAsthmaDataAsync(
            null, null, null, null, null, null,
            null, null, null, null,
            false, triggers, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.AsthmaTriggers, Has.Count.EqualTo(4));
        Assert.That(state.AsthmaTriggers, Contains.Item("dust"));
        Assert.That(state.AsthmaTriggers, Contains.Item("exercise"));
        Assert.That(state.AsthmaTriggers, Contains.Item("cold air"));
        Assert.That(state.AsthmaTriggers, Contains.Item("pet dander"));
    }

    [Test]
    public async Task AsthmaRegistry_EdVisits_PersistsCount()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollAsthmaGrain(patientId);

        await grain.UpdateAsthmaDataAsync(
            null, null, null, null, null, null,
            null, null, null, null,
            false, null, 3);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();
        Assert.That(state.AsthmaEdVisitsLast12Months, Is.EqualTo(3));
    }

    // ── Combined Tests ────────────────────────────────────────────────────────

    [Test]
    public async Task DiabetesEnriched_ExistingFieldsUnchanged()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IClinicalRegistryEntryGrain grain = await EnrollDiabetesGrain(patientId);

        // Update basic diabetes data
        List<string> complications = new() { "Retinopathy", "Neuropathy" };
        await grain.UpdateDiabetesDataAsync(
            DiabetesType.Type2, 7.8m, DateTime.UtcNow.AddDays(-14),
            true, complications);

        // Update enriched fields
        await grain.UpdateDiabetesEnrichedDataAsync(
            100m, DateTime.UtcNow.AddDays(-7),
            20m, DateTime.UtcNow.AddDays(-7),
            128, 82, DateTime.UtcNow.AddDays(-1),
            new DiabetesMedicationStatus { Insulin = true, Metformin = true },
            null, null);

        ClinicalRegistryEntryState state = await grain.GetEntryAsync();

        // Basic diabetes fields should still be intact
        Assert.That(state.DiabetesType, Is.EqualTo(DiabetesType.Type2));
        Assert.That(state.HbA1cPct, Is.EqualTo(7.8m));
        Assert.That(state.IsInsulinDependent, Is.True);
        Assert.That(state.DiabetesComplications, Has.Count.EqualTo(2));
        Assert.That(state.DiabetesComplications, Contains.Item("Retinopathy"));

        // Enriched fields should be set
        Assert.That(state.LdlMgDl, Is.EqualTo(100m));
        Assert.That(state.MicroalbuminMgL, Is.EqualTo(20m));
        Assert.That(state.BloodPressureSystolic, Is.EqualTo(128));
        Assert.That(state.DiabetesMedications!.Insulin, Is.True);
        Assert.That(state.DiabetesMedications.Metformin, Is.True);
    }

    [Test]
    public async Task AsthmaRegistry_CoexistsWithDiabetes()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";

        // Enroll in Diabetes
        IClinicalRegistryEntryGrain dmGrain = await EnrollDiabetesGrain(patientId);
        await dmGrain.UpdateDiabetesDataAsync(
            DiabetesType.Type2, 7.5m, DateTime.UtcNow,
            false, new List<string>());

        // Enroll in Asthma (separate grain key)
        IClinicalRegistryEntryGrain asthmaGrain = await EnrollAsthmaGrain(patientId);
        await asthmaGrain.UpdateAsthmaDataAsync(
            DateTime.UtcNow.AddYears(-3),
            AsthmaSeverity.MildPersistent,
            AsthmaControlLevel.WellControlled,
            null, null, null, null, null,
            "Fluticasone", "Albuterol",
            true, new List<string> { "pollen" }, 0);

        // Verify diabetes grain is independent
        ClinicalRegistryEntryState dmState = await dmGrain.GetEntryAsync();
        Assert.That(dmState.RegistryType, Is.EqualTo(RegistryType.DiabetesMellitus));
        Assert.That(dmState.DiabetesType, Is.EqualTo(DiabetesType.Type2));
        Assert.That(dmState.AsthmaSeverity, Is.Null);

        // Verify asthma grain is independent
        ClinicalRegistryEntryState asthmaState = await asthmaGrain.GetEntryAsync();
        Assert.That(asthmaState.RegistryType, Is.EqualTo(RegistryType.Asthma));
        Assert.That(asthmaState.AsthmaSeverity, Is.EqualTo(AsthmaSeverity.MildPersistent));
        Assert.That(asthmaState.DiabetesType, Is.Null);
    }

    [Test]
    public void RegistryType_Asthma_ValueIs3()
    {
        Assert.That((int)RegistryType.Asthma, Is.EqualTo(3));
    }
}
