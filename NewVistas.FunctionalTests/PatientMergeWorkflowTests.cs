// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Patient Merge via the PatientWorkflowGrain.
/// Verifies the Site Flavor Architecture (Option 4 — Composition) feature gate,
/// clinical data consolidation, and source patient deactivation.
/// Maps to VistA DG MERGE utility (File #15.1).
/// </summary>
[TestFixture]
public class PatientMergeWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private ISiteParametersGrain GetSiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private IPatientIndexGrain GetPatientIndex() =>
        _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    /// <summary>
    /// Helper: create a patient with demographics and register in the patient index.
    /// </summary>
    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain grain = GetPatient(patientId);
        await grain.UpdateDemographicsAsync(name, sex, dob, ssn);

        IPatientIndexGrain index = GetPatientIndex();
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId,
            Name = name,
            DateOfBirth = dob,
            Sex = sex,
            SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty,
            IsActive = true
        });

        return patientId;
    }

    // ─── Test 1: Fails When Feature Disabled ────────────────────────────

    [Test]
    public async Task WorkflowMerge_FailsWhenFeatureDisabled()
    {
        // Arrange — do NOT enable PATIENT_MERGE feature
        string targetId = await CreatePatientAsync("WARD,TOM", "M", new DateTime(1960, 1, 1), "111222333");
        string sourceId = await CreatePatientAsync("WARD,THOMAS", "M", new DateTime(1960, 1, 1), "111222333");

        // Ensure feature is disabled (in case previous tests enabled it)
        await GetSiteParams().DisableFeatureAsync("PATIENT_MERGE");

        // Act
        IPatientWorkflowGrain targetWorkflow = GetWorkflow(targetId);
        PatientMergeResult result = await targetWorkflow.MergePatientAsync(
            sourceId, "Duplicate record", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("not enabled"));
    }

    // ─── Test 2: Succeeds When Feature Enabled ──────────────────────────

    [Test]
    public async Task WorkflowMerge_SucceedsWhenFeatureEnabled()
    {
        // Arrange
        await GetSiteParams().EnableFeatureAsync("PATIENT_MERGE");

        string targetId = await CreatePatientAsync("CLARK,SARAH", "F", new DateTime(1975, 4, 22), "444555666");
        string sourceId = await CreatePatientAsync("CLARK,SARA", "F", new DateTime(1975, 4, 22), "444555666");

        await GetPatient(sourceId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-WF-1",
            Allergen = "Amoxicillin",
            AllergenType = "Drug"
        });

        // Act
        IPatientWorkflowGrain targetWorkflow = GetWorkflow(targetId);
        PatientMergeResult result = await targetWorkflow.MergePatientAsync(
            sourceId, "Duplicate record", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.MergeId, Does.StartWith("MERGE:"));
    }

    // ─── Test 3: Merges All Clinical Data ───────────────────────────────

    [Test]
    public async Task WorkflowMerge_MergesAllClinicalData()
    {
        // Arrange
        await GetSiteParams().EnableFeatureAsync("PATIENT_MERGE");

        string targetId = await CreatePatientAsync("ADAMS,EVE", "F", new DateTime(1982, 6, 15), "777888999");
        string sourceId = await CreatePatientAsync("ADAMS,EVELYN", "F", new DateTime(1982, 6, 15), "777888999");

        // Target has existing data
        await GetPatient(targetId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-T1",
            Allergen = "Penicillin",
            AllergenType = "Drug"
        });
        await GetPatient(targetId).AddProblemAsync(new ProblemEntry
        {
            ProblemId = "PROB-T1",
            Diagnosis = "Hypertension",
            DiagnosisCode = "I10",
            Status = "ACTIVE"
        });
        await GetPatient(targetId).AddLabTestIdAsync("LAB-T1");

        // Source has different data
        await GetPatient(sourceId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-S1",
            Allergen = "Sulfa",
            AllergenType = "Drug"
        });
        await GetPatient(sourceId).AddProblemAsync(new ProblemEntry
        {
            ProblemId = "PROB-S1",
            Diagnosis = "Diabetes Type 2",
            DiagnosisCode = "E11.9",
            Status = "ACTIVE"
        });
        await GetPatient(sourceId).AddImmunizationAsync(new ImmunizationEntry
        {
            ImmunizationId = "IMM-S1",
            ImmunizationName = "COVID-19 Vaccine",
            Series = "PRIMARY"
        });
        await GetPatient(sourceId).AddLabTestIdAsync("LAB-S1");
        await GetPatient(sourceId).AddOrderIdAsync("ORDER-S1");

        // Act
        IPatientWorkflowGrain targetWorkflow = GetWorkflow(targetId);
        PatientMergeResult result = await targetWorkflow.MergePatientAsync(
            sourceId, "Duplicate record", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);

        // Verify consolidated allergies
        List<AllergyEntry> allergies = await GetPatient(targetId).GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(2));
        Assert.That(allergies.Select(a => a.Allergen), Does.Contain("Penicillin"));
        Assert.That(allergies.Select(a => a.Allergen), Does.Contain("Sulfa"));

        // Verify consolidated problems
        List<ProblemEntry> problems = await GetPatient(targetId).GetProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(2));
        Assert.That(problems.Select(p => p.Diagnosis), Does.Contain("Hypertension"));
        Assert.That(problems.Select(p => p.Diagnosis), Does.Contain("Diabetes Type 2"));

        // Verify consolidated immunizations
        List<ImmunizationEntry> immunizations = await GetPatient(targetId).GetImmunizationsAsync();
        Assert.That(immunizations, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(immunizations.Select(i => i.ImmunizationName), Does.Contain("COVID-19 Vaccine"));

        // Verify consolidated lab IDs
        List<string> labIds = await GetPatient(targetId).GetLabTestIdsAsync();
        Assert.That(labIds, Does.Contain("LAB-T1"));
        Assert.That(labIds, Does.Contain("LAB-S1"));

        // Verify consolidated order IDs
        List<string> orderIds = await GetPatient(targetId).GetOrderIdsAsync();
        Assert.That(orderIds, Does.Contain("ORDER-S1"));

        // Verify items moved counts
        Assert.That(result.ItemsMoved, Does.ContainKey("Allergies"));
        Assert.That(result.ItemsMoved["Allergies"], Is.EqualTo(1));
        Assert.That(result.ItemsMoved, Does.ContainKey("Problems"));
        Assert.That(result.ItemsMoved["Problems"], Is.EqualTo(1));
        Assert.That(result.ItemsMoved, Does.ContainKey("Immunizations"));
        Assert.That(result.ItemsMoved["Immunizations"], Is.EqualTo(1));
        Assert.That(result.ItemsMoved, Does.ContainKey("LabTests"));
        Assert.That(result.ItemsMoved["LabTests"], Is.EqualTo(1));
        Assert.That(result.ItemsMoved, Does.ContainKey("Orders"));
        Assert.That(result.ItemsMoved["Orders"], Is.EqualTo(1));
    }

    // ─── Test 4: Source Patient Deactivated ─────────────────────────────

    [Test]
    public async Task WorkflowMerge_SourcePatientDeactivated()
    {
        // Arrange
        await GetSiteParams().EnableFeatureAsync("PATIENT_MERGE");

        string targetId = await CreatePatientAsync("BAKER,DAN", "M", new DateTime(1968, 10, 3), "333444555");
        string sourceId = await CreatePatientAsync("BAKER,DANIEL", "M", new DateTime(1968, 10, 3), "333444555");

        // Act
        IPatientWorkflowGrain targetWorkflow = GetWorkflow(targetId);
        PatientMergeResult result = await targetWorkflow.MergePatientAsync(
            sourceId, "Duplicate record", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);

        PatientState sourceState = await GetPatient(sourceId).GetPatientAsync();
        Assert.That(sourceState.MergedIntoPatientId, Is.EqualTo(targetId));
        Assert.That(sourceState.IsActive, Is.False);
    }
}
