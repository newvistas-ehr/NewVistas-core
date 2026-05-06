// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Dietetics — FH File #115.
/// Diet orders are now embedded on the patient grain as DieteticsEntry.
/// Tests exercise the workflow grain methods for diet orders, nutrition goals,
/// fluid restrictions, tube feeding, NPO, assessments, and full lifecycle workflows.
/// </summary>
[TestFixture]
public class DieteticsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private async Task<string> CreateStandardDietOrderAsync(IPatientWorkflowGrain w,
        string dietType = "REGULAR")
    {
        return await w.CreateDietOrderAsync(
            dietType, "Regular diet",
            new List<string> { "LOW SODIUM" }, "REGULAR", "THIN",
            "2000 kcal", "No special instructions",
            DateTime.UtcNow,
            "PROV-001", "Dr. Nutrition", null);
    }

    // ─── 1. Create Diet Order ────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanCreateDietOrder()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await CreateStandardDietOrderAsync(w);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Status, Is.EqualTo("ACTIVE"));
        Assert.That(entry.DietType, Is.EqualTo("REGULAR"));
        Assert.That(entry.CurrentDiet, Is.EqualTo("Regular diet"));
        Assert.That(entry.CalorieLevel, Is.EqualTo("2000 kcal"));
    }

    // ─── 2. Get Diet Order ───────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanGetDietOrder()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await CreateStandardDietOrderAsync(w);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DieteticsId, Is.Not.Empty);
        Assert.That(entry.Modifications, Has.Count.EqualTo(1));
        Assert.That(entry.Modifications, Contains.Item("LOW SODIUM"));
    }

    // ─── 3. Discontinue ─────────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanDiscontinue()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.DiscontinueDietOrderAsync(id);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Status, Is.EqualTo("DISCONTINUED"));
    }

    // ─── 4. Set Nutrition Goals ──────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanSetNutritionGoals()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.SetDietNutritionGoalsAsync(id, 1800, 75.0m);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.CalorieTarget, Is.EqualTo(1800));
        Assert.That(entry.TargetWeight, Is.EqualTo(75.0m));
    }

    // ─── 5. Set Fluid Restriction ────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanSetFluidRestriction()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.SetDietFluidRestrictionAsync(id, 1500);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.FluidRestrictionMl, Is.EqualTo(1500));
    }

    // ─── 6. Set Texture Consistency ──────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanSetTextureConsistency()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.SetDietTextureConsistencyAsync(id, "PUREED");

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.TextureConsistency, Is.EqualTo("PUREED"));
    }

    // ─── 7. Set Tube Feeding ─────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanSetTubeFeeding()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.SetDietTubeFeedingAsync(id, true, "Jevity 1.5 Cal", 60.0m);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.IsTubeFeeding, Is.True);
        Assert.That(entry.TubeFeedingFormula, Is.EqualTo("Jevity 1.5 Cal"));
        Assert.That(entry.TubeFeedingRateMlHr, Is.EqualTo(60.0m));
    }

    // ─── 8. Set NPO ─────────────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanSetNPO()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);
        DateTime npoStart = DateTime.UtcNow;
        DateTime npoEnd = DateTime.UtcNow.AddHours(12);

        await w.SetDietNPOAsync(id, true, npoStart, npoEnd);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.IsNPO, Is.True);
        Assert.That(entry.NPOStartDate, Is.Not.Null);
        Assert.That(entry.NPOStartDate!.Value, Is.EqualTo(npoStart).Within(TimeSpan.FromSeconds(1)));
        Assert.That(entry.NPOEndDate, Is.Not.Null);
        Assert.That(entry.NPOEndDate!.Value, Is.EqualTo(npoEnd).Within(TimeSpan.FromSeconds(1)));
    }

    // ─── 9. Record Meal Preference ──────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanRecordMealPreference()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.RecordDietMealPreferenceAsync(id, "Prefers warm meals. No onions. Extra fruit at breakfast.");

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.MealPreferences, Is.EqualTo("Prefers warm meals. No onions. Extra fruit at breakfast."));
    }

    // ─── 10. Record Nutrition Assessment ─────────────────────────────────────

    [Test]
    public async Task Dietetics_CanRecordNutritionAssessment()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.RecordDietNutritionAssessmentAsync(id, 22.5m, "Jane Dietitian RD");

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.NutritionAssessmentScore, Is.EqualTo(22.5m));
        Assert.That(entry.AssessedByName, Is.EqualTo("Jane Dietitian RD"));
        Assert.That(entry.NutritionAssessmentDate, Is.Not.Null);
    }

    // ─── 11. Record BMI ──────────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CanRecordBMI()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.RecordDietBMIAsync(id, 28.3m);

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.CurrentBMI, Is.EqualTo(28.3m));
    }

    // ─── 12. Set Allergy Considerations ──────────────────────────────────────

    [Test]
    public async Task Dietetics_CanSetAllergyConsiderations()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await CreateStandardDietOrderAsync(w);

        await w.SetDietAllergyConsiderationsAsync(id, "Peanut allergy - avoid all tree nuts. Lactose intolerant.");

        DieteticsEntry? entry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.AllergyConsiderations, Is.EqualTo("Peanut allergy - avoid all tree nuts. Lactose intolerant."));
    }

    // ─── 13. List Diet Orders ────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_ListReturnsAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await CreateStandardDietOrderAsync(w, "REGULAR");
        await CreateStandardDietOrderAsync(w, "CARDIAC");

        List<DieteticsSummary> list = await w.GetDietOrdersAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    // ─── 14. Patient Linkage ──────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_LinksToPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await CreateStandardDietOrderAsync(w);

        List<DieteticsEntry> entries = await GetPatient(patientId).GetDietOrdersAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].DieteticsId, Is.Not.Empty);
    }

    // ─── 15. Multiple Patients Independent ────────────────────────────────────

    [Test]
    public async Task Dietetics_MultiplePatients_Independent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await CreateStandardDietOrderAsync(w1);

        List<DieteticsSummary> list2 = await w2.GetDietOrdersAsync();
        Assert.That(list2, Is.Empty);
    }

    // ─── 16. Full Workflow ───────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_FullWorkflow()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Create diet order
        string id = await CreateStandardDietOrderAsync(w, "REGULAR");
        DieteticsEntry? s1 = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(s1!.Status, Is.EqualTo("ACTIVE"));

        // Set nutrition goals
        await w.SetDietNutritionGoalsAsync(id, 1800, 70.0m);
        DieteticsEntry? s2 = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(s2!.CalorieTarget, Is.EqualTo(1800));
        Assert.That(s2.TargetWeight, Is.EqualTo(70.0m));

        // Set fluid restriction
        await w.SetDietFluidRestrictionAsync(id, 1500);

        // Set texture
        await w.SetDietTextureConsistencyAsync(id, "MECHANICAL_SOFT");

        // Set tube feeding
        await w.SetDietTubeFeedingAsync(id, true, "Osmolite 1.2 Cal", 45.0m);
        DieteticsEntry? s3 = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(s3!.IsTubeFeeding, Is.True);
        Assert.That(s3.TubeFeedingFormula, Is.EqualTo("Osmolite 1.2 Cal"));

        // NPO off
        await w.SetDietNPOAsync(id, false, null, null);
        DieteticsEntry? s4 = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(s4!.IsNPO, Is.False);

        // Record assessment
        await w.RecordDietNutritionAssessmentAsync(id, 19.5m, "Dr. Nutrition RD");
        DieteticsEntry? s5 = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(s5!.NutritionAssessmentScore, Is.EqualTo(19.5m));

        // Record BMI
        await w.RecordDietBMIAsync(id, 24.1m);

        // Record meal preferences
        await w.RecordDietMealPreferenceAsync(id, "Vegetarian. Prefers smaller, more frequent meals.");

        // Set allergy considerations
        await w.SetDietAllergyConsiderationsAsync(id, "Gluten sensitivity. Avoid wheat-based products.");

        // Final assertions
        DieteticsEntry? finalEntry = await GetPatient(patientId).GetDietOrderAsync(id);
        Assert.That(finalEntry, Is.Not.Null);
        Assert.That(finalEntry!.Status, Is.EqualTo("ACTIVE"));
        Assert.That(finalEntry.CalorieTarget, Is.EqualTo(1800));
        Assert.That(finalEntry.FluidRestrictionMl, Is.EqualTo(1500));
        Assert.That(finalEntry.TextureConsistency, Is.EqualTo("MECHANICAL_SOFT"));
        Assert.That(finalEntry.IsTubeFeeding, Is.True);
        Assert.That(finalEntry.CurrentBMI, Is.EqualTo(24.1m));
        Assert.That(finalEntry.MealPreferences, Does.Contain("Vegetarian"));
        Assert.That(finalEntry.AllergyConsiderations, Does.Contain("Gluten"));
    }
}
