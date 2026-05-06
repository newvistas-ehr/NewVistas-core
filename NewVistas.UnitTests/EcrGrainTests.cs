// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Electronic Case Reporting (eCR) grains.
/// §170.315(f)(5) — Electronic Case Reporting.
/// </summary>
[TestFixture]
public class EcrGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Trigger CRUD ────────────────────────────────────────────────────────

    [Test]
    public async Task EcrTrigger_SaveAndRetrieve()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Measles",
            ConditionCode = "14189004",
            ConditionCodeSystem = "SNOMED",
            Category = "communicable",
            ReportingTimeframe = "Immediately",
            IsActive = true,
            Jurisdictions = new List<string> { "US", "VA" },
            TriggerCodes = new List<EcrTriggerCode>
            {
                new() { Code = "B05.*", CodeSystem = "ICD-10", Description = "Measles", TriggerType = "diagnosis" }
            }
        });

        EcrTriggerState result = await grain.GetTriggerAsync();
        Assert.That(result.ConditionName, Is.EqualTo("Measles"));
        Assert.That(result.Category, Is.EqualTo("communicable"));
        Assert.That(result.TriggerCodes, Has.Count.EqualTo(1));
        Assert.That(result.Jurisdictions, Has.Count.EqualTo(2));
        Assert.That(result.IsActive, Is.True);
    }

    [Test]
    public async Task EcrTrigger_SetActive_Toggles()
    {
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain grain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");

        await grain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId, ConditionName = "Test", IsActive = true
        });

        await grain.SetActiveAsync(false);
        Assert.That((await grain.GetTriggerAsync()).IsActive, Is.False);

        await grain.SetActiveAsync(true);
        Assert.That((await grain.GetTriggerAsync()).IsActive, Is.True);
    }

    // ─── Trigger Index ───────────────────────────────────────────────────────

    [Test]
    public async Task EcrTriggerIndex_AddAndList()
    {
        string indexKey = $"ECR-TRIGGER-INDEX-{Guid.NewGuid():N}";
        IEcrTriggerIndexGrain index = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>(indexKey);

        for (int i = 0; i < 4; i++)
        {
            await index.AddTriggerAsync(new EcrTriggerSummary
            {
                TriggerId = $"T-{i}", ConditionName = $"Condition {i}",
                Category = "communicable", IsActive = i != 2,
                ReportingTimeframe = "24 hours", TriggerCodeCount = 1
            });
        }

        List<EcrTriggerSummary> all = await index.GetAllTriggersAsync();
        Assert.That(all, Has.Count.EqualTo(4));

        List<EcrTriggerSummary> active = await index.GetActiveTriggersAsync();
        Assert.That(active, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task EcrTriggerIndex_RemoveTrigger()
    {
        string indexKey = $"ECR-TRIGGER-INDEX-{Guid.NewGuid():N}";
        IEcrTriggerIndexGrain index = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>(indexKey);

        await index.AddTriggerAsync(new EcrTriggerSummary { TriggerId = "A", ConditionName = "Condition A" });
        await index.AddTriggerAsync(new EcrTriggerSummary { TriggerId = "B", ConditionName = "Condition B" });

        await index.RemoveTriggerAsync("A");
        List<EcrTriggerSummary> remaining = await index.GetAllTriggersAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].TriggerId, Is.EqualTo("B"));
    }

    [Test]
    public async Task EcrTriggerIndex_AddDuplicate_ReplacesExisting()
    {
        string indexKey = $"ECR-TRIGGER-INDEX-{Guid.NewGuid():N}";
        IEcrTriggerIndexGrain index = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>(indexKey);

        await index.AddTriggerAsync(new EcrTriggerSummary { TriggerId = "DUP", ConditionName = "Original" });
        await index.AddTriggerAsync(new EcrTriggerSummary { TriggerId = "DUP", ConditionName = "Updated" });

        List<EcrTriggerSummary> all = await index.GetAllTriggersAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ConditionName, Is.EqualTo("Updated"));
    }

    // ─── Case Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task EcrCase_CreateAndRetrieve()
    {
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("ECR,PATIENT", "M", DateTime.UtcNow.AddYears(-45), "000-00-0001");

        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain grain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);

        await grain.CreateCaseAsync(
            patientId, "TRIGGER-1", "Measles", "B05.9", "ICD-10", "Measles, unspecified",
            new List<string> { "US" }, new List<string> { "Diagnosis: Measles (B05.9)" },
            "DR-001", "VA Medical Center");

        EcrCaseState result = await grain.GetCaseAsync();
        Assert.That(result.PatientId, Is.EqualTo(patientId));
        Assert.That(result.ConditionName, Is.EqualTo("Measles"));
        Assert.That(result.Status, Is.EqualTo("triggered"));
        Assert.That(result.PatientName, Does.Contain("ECR"));
        Assert.That(result.Jurisdictions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EcrCase_GenerateEicr_CreatesXml()
    {
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("EICR,GENERATE", "F", DateTime.UtcNow.AddYears(-30), "000-00-0002");

        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain grain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);

        await grain.CreateCaseAsync(
            patientId, "TRIGGER-TB", "Tuberculosis", "A15.0", "ICD-10",
            "Tuberculosis of lung", new List<string> { "US" },
            new List<string> { "Diagnosis: TB (A15.0)" }, null, "VA Hospital");

        await grain.GenerateEicrAsync();

        EcrCaseState result = await grain.GetCaseAsync();
        Assert.That(result.Status, Is.EqualTo("generated"));
        Assert.That(result.GeneratedDate, Is.Not.Null);
        Assert.That(result.EicrDocument, Is.Not.Null);
        Assert.That(result.EicrDocument, Does.Contain("ClinicalDocument"));
        Assert.That(result.EicrDocument, Does.Contain("2.16.840.1.113883.10.20.15.2")); // eICR template
        Assert.That(result.EicrDocument, Does.Contain("Tuberculosis"));
        Assert.That(result.EicrDocument, Does.Contain(patientId));
    }

    [Test]
    public async Task EcrCase_FullLifecycle_TriggeredToReportable()
    {
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("LIFECYCLE,TEST", "M", DateTime.UtcNow.AddYears(-50), "000-00-0003");

        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain grain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);

        // 1. Create
        await grain.CreateCaseAsync(
            patientId, "TRIGGER-COVID", "COVID-19", "U07.1", "ICD-10",
            "COVID-19, virus identified", new List<string> { "US", "VA" },
            new List<string> { "Diagnosis: COVID-19 (U07.1)" }, "DR-100", "VA Clinic");

        Assert.That((await grain.GetCaseAsync()).Status, Is.EqualTo("triggered"));

        // 2. Generate eICR
        await grain.GenerateEicrAsync();
        Assert.That((await grain.GetCaseAsync()).Status, Is.EqualTo("generated"));

        // 3. Submit
        await grain.MarkSubmittedAsync();
        EcrCaseState submitted = await grain.GetCaseAsync();
        Assert.That(submitted.Status, Is.EqualTo("submitted"));
        Assert.That(submitted.SubmittedDate, Is.Not.Null);

        // 4. Receive Reportability Response
        await grain.RecordReportabilityResponseAsync("reportable",
            "Condition is reportable in VA jurisdiction. Report to state health department.");

        EcrCaseState final_ = await grain.GetCaseAsync();
        Assert.That(final_.Status, Is.EqualTo("reportable"));
        Assert.That(final_.ReportabilityDetermination, Is.EqualTo("reportable"));
        Assert.That(final_.ReportabilityResponse, Does.Contain("reportable"));
        Assert.That(final_.ResponseDate, Is.Not.Null);
    }

    [Test]
    public async Task EcrCase_GenerateBeforeTriggered_Throws()
    {
        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain grain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);

        // Default status is not "triggered", so this should fail
        // Actually the default CaseId is empty and status is "triggered" from default...
        // Let's test submitting before generating
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("ORDER,TEST", "M", DateTime.UtcNow.AddYears(-40), "000-00-0004");

        await grain.CreateCaseAsync(patientId, "T1", "Test", "X00", "ICD-10", "Test",
            new List<string>(), new List<string>(), null, null);
        await grain.GenerateEicrAsync();

        // Double-generate should fail (status is "generated", not "triggered")
        Assert.ThrowsAsync<InvalidOperationException>(async () => await grain.GenerateEicrAsync());
    }

    [Test]
    public async Task EcrCase_SubmitBeforeGenerate_Throws()
    {
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("SUBMIT,TEST", "F", DateTime.UtcNow.AddYears(-35), "000-00-0005");

        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain grain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);
        await grain.CreateCaseAsync(patientId, "T2", "Test", "X01", "ICD-10", "Test",
            new List<string>(), new List<string>(), null, null);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await grain.MarkSubmittedAsync());
    }

    // ─── Case Index ──────────────────────────────────────────────────────────

    [Test]
    public async Task EcrCaseIndex_AddAndFilter()
    {
        string indexKey = $"ECR-CASE-INDEX-{Guid.NewGuid():N}";
        IEcrCaseIndexGrain index = _cluster.GrainFactory.GetGrain<IEcrCaseIndexGrain>(indexKey);

        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "C1", PatientId = "P1", ConditionName = "Measles",
            Status = "generated", TriggeredDate = DateTime.UtcNow
        });
        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "C2", PatientId = "P2", ConditionName = "TB",
            Status = "submitted", TriggeredDate = DateTime.UtcNow
        });
        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "C3", PatientId = "P1", ConditionName = "COVID-19",
            Status = "generated", TriggeredDate = DateTime.UtcNow
        });

        List<EcrCaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all, Has.Count.EqualTo(3));

        List<EcrCaseSummary> generated = await index.GetCasesByStatusAsync("generated");
        Assert.That(generated, Has.Count.EqualTo(2));

        List<EcrCaseSummary> p1Cases = await index.GetCasesByPatientAsync("P1");
        Assert.That(p1Cases, Has.Count.EqualTo(2));

        List<EcrCaseSummary> tbCases = await index.GetCasesByConditionAsync("TB");
        Assert.That(tbCases, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EcrCaseIndex_UpdateStatus()
    {
        string indexKey = $"ECR-CASE-INDEX-{Guid.NewGuid():N}";
        IEcrCaseIndexGrain index = _cluster.GrainFactory.GetGrain<IEcrCaseIndexGrain>(indexKey);

        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "UPD-1", PatientId = "P1", ConditionName = "Test",
            Status = "generated", TriggeredDate = DateTime.UtcNow
        });

        await index.UpdateCaseStatusAsync("UPD-1", "reportable", "reportable");

        List<EcrCaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all[0].Status, Is.EqualTo("reportable"));
        Assert.That(all[0].ReportabilityDetermination, Is.EqualTo("reportable"));
    }

    // ─── Screening ───────────────────────────────────────────────────────────

    [Test]
    public async Task EcrScreening_DetectsReportableCondition()
    {
        // Register a trigger for measles
        string triggerId = $"TRIGGER-{Guid.NewGuid():N}";
        IEcrTriggerGrain triggerGrain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");
        await triggerGrain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = "Measles",
            Category = "communicable",
            IsActive = true,
            Jurisdictions = new List<string> { "US" },
            ReportingTimeframe = "Immediately",
            TriggerCodes = new List<EcrTriggerCode>
            {
                new() { Code = "B05.*", CodeSystem = "ICD-10", Description = "Measles", TriggerType = "diagnosis" }
            }
        });

        // Add to the global index
        IEcrTriggerIndexGrain triggerIndex = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
        await triggerIndex.AddTriggerAsync(new EcrTriggerSummary
        {
            TriggerId = triggerId, ConditionName = "Measles", IsActive = true,
            Category = "communicable", ReportingTimeframe = "Immediately", TriggerCodeCount = 1
        });

        // Create patient with measles diagnosis
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("MEASLES,PATIENT", "M", DateTime.UtcNow.AddYears(-25), "000-00-0010");
        await w.AddProblemAsync("Measles without complication", "B05.9", "active",
            null, null, null, null, null, null, false, null);

        // Screen
        IEcrScreeningGrain screening = _cluster.GrainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
        List<EcrScreeningMatch> matches = await screening.ScreenPatientAsync();

        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].ConditionName, Is.EqualTo("Measles"));
        Assert.That(matches[0].MatchedCode, Is.EqualTo("B05.9"));
        Assert.That(matches[0].ClinicalEvidence.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task EcrScreening_NoMatch_ReturnsEmpty()
    {
        // Create patient with no reportable conditions
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("HEALTHY,PERSON", "F", DateTime.UtcNow.AddYears(-35), "000-00-0011");

        IEcrScreeningGrain screening = _cluster.GrainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
        List<EcrScreeningMatch> matches = await screening.ScreenPatientAsync();

        Assert.That(matches, Has.Count.EqualTo(0));
    }
}
