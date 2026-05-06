// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Decision Support Interventions (DSI) end-to-end workflows.
/// §170.315(b)(11) — Decision Support Interventions with HTI-1 transparency.
/// </summary>
[TestFixture]
public class DsiWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Full Lifecycle ───────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_CreateIntervention_EvaluatePatient_RecordResponse()
    {
        string patientId = $"DSI-FUNC-{Guid.NewGuid():N}";
        string intId = Guid.NewGuid().ToString("N");

        // 1. Create patient with diabetes
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.AddProblemAsync("Type 2 Diabetes Mellitus", "E11.65", "Active", "Chronic",
            DateTime.UtcNow.AddYears(-3), "DR001", "Dr. Smith", null, null, false, null);

        // 2. Register evidence-based intervention
        IDsiInterventionGrain intGrain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId}");
        await intGrain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId,
            Title = "Diabetic Retinopathy Screening Reminder",
            Description = "Annual eye exam for diabetic patients",
            InterventionType = "evidence-based",
            ClinicalDomain = "preventive",
            IsActive = true,
            SourceCitation = "ADA Standards of Care 2024, Section 12",
            Developer = "NewVistas Clinical Team",
            Severity = "info",
            RecommendedAction = "Refer patient for dilated eye exam if not done in past 12 months",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "E11.*", Operator = "exists",
                    Description = "Type 2 Diabetes (any E11.x code)" }
            }
        });

        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId, Title = "Diabetic Retinopathy Screening Reminder",
            InterventionType = "evidence-based", ClinicalDomain = "preventive",
            IsActive = true, Severity = "info", Developer = "NewVistas Clinical Team"
        });

        // 3. Evaluate patient — should trigger
        IDsiEvaluationGrain evalGrain = _cluster.GrainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
        List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

        DsiEvaluationResult alert = results.First(r => r.InterventionId == intId);
        Assert.That(alert.Title, Is.EqualTo("Diabetic Retinopathy Screening Reminder"));
        Assert.That(alert.SourceCitation, Is.EqualTo("ADA Standards of Care 2024, Section 12"));
        Assert.That(alert.TriggerEvidence, Has.Count.GreaterThan(0));
        Assert.That(alert.PredictiveTransparency, Is.Null); // evidence-based, no HTI-1

        // 4. Record firing event
        string eventId = $"DSI-EVENT:{Guid.NewGuid():N}";
        IDsiEventGrain eventGrain = _cluster.GrainFactory.GetGrain<IDsiEventGrain>(eventId);
        await eventGrain.RecordFiringAsync(
            alert.InterventionId, alert.Title, alert.InterventionType,
            patientId, "DR001", alert.RecommendedAction,
            alert.Severity, alert.TriggerEvidence, alert.SourceCitation);

        IDsiEventIndexGrain eventIndex = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
        await eventIndex.AddEventAsync(new DsiEventSummary
        {
            EventId = eventId, InterventionId = intId,
            InterventionTitle = alert.Title, PatientId = patientId,
            FiredDate = DateTime.UtcNow, Severity = "info", UserResponse = "pending"
        });

        // 5. Verify pending
        List<DsiEventSummary> pending = await eventIndex.GetPendingEventsAsync();
        Assert.That(pending.Any(e => e.EventId == eventId), Is.True);

        // 6. Clinician accepts the recommendation
        await eventGrain.RecordResponseAsync("accepted", null);
        await eventIndex.UpdateResponseAsync(eventId, "accepted");

        // 7. Verify no longer pending
        pending = await eventIndex.GetPendingEventsAsync();
        Assert.That(pending.Any(e => e.EventId == eventId), Is.False);

        DsiEventState finalEvent = await eventGrain.GetEventAsync();
        Assert.That(finalEvent.UserResponse, Is.EqualTo("accepted"));
    }

    [Test]
    public async Task PredictiveDsi_IncludesHti1Transparency()
    {
        string patientId = $"DSI-FUNC-{Guid.NewGuid():N}";
        string intId = Guid.NewGuid().ToString("N");

        // Patient with elevated vitals
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                { "Temperature", "39.2" },
                { "HeartRate", "110" }
            }, null);

        // Register predictive DSI with HTI-1 transparency
        IDsiInterventionGrain intGrain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId}");
        await intGrain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId,
            Title = "Sepsis Risk Predictor",
            InterventionType = "predictive",
            ClinicalDomain = "diagnostic",
            IsActive = true,
            SourceCitation = "Internal ML Model v2.3",
            Developer = "AI Health Labs",
            ModelPurpose = "Predict sepsis onset within 6 hours based on vital signs",
            PerformanceMetrics = "AUROC 0.89, Sensitivity 0.85, Specificity 0.82",
            KnownLimitations = "Reduced accuracy in pediatric populations under age 5",
            FairnessAssessment = "Validated across age, sex, race — no significant disparities detected",
            InputDataRequirements = "Temperature, Heart Rate, Respiratory Rate, WBC, Lactate",
            OutputDescription = "Probability score 0-1 with risk category: low (<0.3), medium (0.3-0.7), high (>0.7)",
            Severity = "critical",
            RecommendedAction = "Consider blood cultures and empiric antibiotics",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Vital", ValueSetOrCode = "Temperature", Operator = "greater-than", ComparisonValue = "38.3" },
                new() { DataSource = "Vital", ValueSetOrCode = "HeartRate", Operator = "greater-than", ComparisonValue = "90" }
            }
        });

        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId, Title = "Sepsis Risk Predictor",
            InterventionType = "predictive", IsActive = true, Severity = "critical"
        });

        // Evaluate — should trigger with HTI-1 transparency
        IDsiEvaluationGrain evalGrain = _cluster.GrainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
        List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

        DsiEvaluationResult alert = results.First(r => r.InterventionId == intId);
        Assert.That(alert.InterventionType, Is.EqualTo("predictive"));
        Assert.That(alert.PredictiveTransparency, Is.Not.Null);
        Assert.That(alert.PredictiveTransparency!.ModelPurpose, Does.Contain("sepsis"));
        Assert.That(alert.PredictiveTransparency.Developer, Is.EqualTo("AI Health Labs"));
        Assert.That(alert.PredictiveTransparency.PerformanceMetrics, Does.Contain("AUROC"));
        Assert.That(alert.PredictiveTransparency.KnownLimitations, Does.Contain("pediatric"));
        Assert.That(alert.PredictiveTransparency.FairnessAssessment, Does.Contain("no significant disparities"));
        Assert.That(alert.PredictiveTransparency.InputDataRequirements, Does.Contain("Temperature"));
        Assert.That(alert.PredictiveTransparency.OutputDescription, Does.Contain("Probability"));
    }

    [Test]
    public async Task MultipleInterventions_EvaluatePatientWithMultipleConditions()
    {
        string patientId = $"DSI-FUNC-{Guid.NewGuid():N}";
        string intId1 = Guid.NewGuid().ToString("N");
        string intId2 = Guid.NewGuid().ToString("N");

        // Patient with diabetes and hypertension
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.AddProblemAsync("Type 2 Diabetes", "E11.9", "Active", "Chronic",
            DateTime.UtcNow, null, null, null, null, false, null);
        await workflow.AddProblemAsync("Hypertension", "I10", "Active", "Chronic",
            DateTime.UtcNow, null, null, null, null, false, null);

        // Two independent interventions
        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");

        IDsiInterventionGrain int1 = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId1}");
        await int1.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId1, Title = "Diabetes A1c Check",
            InterventionType = "evidence-based", IsActive = true,
            SourceCitation = "ADA 2024", Severity = "info",
            RecommendedAction = "Order HbA1c",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "E11.*", Operator = "exists" }
            }
        });
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId1, Title = "Diabetes A1c Check",
            InterventionType = "evidence-based", IsActive = true, Severity = "info"
        });

        IDsiInterventionGrain int2 = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId2}");
        await int2.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId2, Title = "HTN BP Goal Check",
            InterventionType = "evidence-based", IsActive = true,
            SourceCitation = "JNC 8 Guidelines", Severity = "warning",
            RecommendedAction = "Verify BP at goal <130/80",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "I10", Operator = "exists" }
            }
        });
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId2, Title = "HTN BP Goal Check",
            InterventionType = "evidence-based", IsActive = true, Severity = "warning"
        });

        // Evaluate — should trigger both
        IDsiEvaluationGrain evalGrain = _cluster.GrainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
        List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

        Assert.That(results.Any(r => r.InterventionId == intId1), Is.True);
        Assert.That(results.Any(r => r.InterventionId == intId2), Is.True);
    }

    [Test]
    public async Task OverrideWorkflow_RecordsReasonInAuditTrail()
    {
        string eventId = $"DSI-EVENT:{Guid.NewGuid():N}";

        // Record a firing
        IDsiEventGrain eventGrain = _cluster.GrainFactory.GetGrain<IDsiEventGrain>(eventId);
        await eventGrain.RecordFiringAsync(
            "INT-OVERRIDE", "Drug Allergy Alert", "evidence-based",
            "PAT-OVERRIDE", "DR-OVERRIDE", "Discontinue penicillin",
            "critical", new List<string> { "Allergy: Penicillin (severe)" },
            "Cross-reactivity Database");

        IDsiEventIndexGrain eventIndex = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
        await eventIndex.AddEventAsync(new DsiEventSummary
        {
            EventId = eventId, InterventionId = "INT-OVERRIDE",
            InterventionTitle = "Drug Allergy Alert", PatientId = "PAT-OVERRIDE",
            FiredDate = DateTime.UtcNow, Severity = "critical", UserResponse = "pending"
        });

        // Clinician overrides with documented reason
        await eventGrain.RecordResponseAsync("overridden",
            "Patient previously tolerated amoxicillin without reaction. Low cross-reactivity risk per allergist consult.");
        await eventIndex.UpdateResponseAsync(eventId, "overridden");

        // Verify audit trail
        DsiEventState evt = await eventGrain.GetEventAsync();
        Assert.That(evt.UserResponse, Is.EqualTo("overridden"));
        Assert.That(evt.OverrideReason, Does.Contain("allergist consult"));
        Assert.That(evt.ResponseDate, Is.Not.Null);

        // Verify index updated
        List<DsiEventSummary> pending = await eventIndex.GetPendingEventsAsync();
        Assert.That(pending.Any(e => e.EventId == eventId), Is.False);
    }

    [Test]
    public async Task EventIndex_DashboardQueries_PatientAndIntervention()
    {
        IDsiEventIndexGrain eventIndex = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>(
            $"DSI-EVENT-INDEX-DASH-{Guid.NewGuid():N}");

        // Add events for multiple patients and interventions
        await eventIndex.AddEventAsync(new DsiEventSummary
        {
            EventId = "DASH-1", InterventionId = "INT-DM", InterventionTitle = "Diabetes Alert",
            PatientId = "PAT-DASH-A", FiredDate = DateTime.UtcNow.AddHours(-2), Severity = "info", UserResponse = "accepted"
        });
        await eventIndex.AddEventAsync(new DsiEventSummary
        {
            EventId = "DASH-2", InterventionId = "INT-HTN", InterventionTitle = "HTN Alert",
            PatientId = "PAT-DASH-A", FiredDate = DateTime.UtcNow.AddHours(-1), Severity = "warning", UserResponse = "pending"
        });
        await eventIndex.AddEventAsync(new DsiEventSummary
        {
            EventId = "DASH-3", InterventionId = "INT-DM", InterventionTitle = "Diabetes Alert",
            PatientId = "PAT-DASH-B", FiredDate = DateTime.UtcNow, Severity = "info", UserResponse = "overridden"
        });

        // All events
        List<DsiEventSummary> all = await eventIndex.GetAllEventsAsync();
        Assert.That(all, Has.Count.EqualTo(3));

        // By patient
        List<DsiEventSummary> patA = await eventIndex.GetEventsByPatientAsync("PAT-DASH-A");
        Assert.That(patA, Has.Count.EqualTo(2));

        // By intervention
        List<DsiEventSummary> dmAlerts = await eventIndex.GetEventsByInterventionAsync("INT-DM");
        Assert.That(dmAlerts, Has.Count.EqualTo(2));

        // Pending only
        List<DsiEventSummary> pending = await eventIndex.GetPendingEventsAsync();
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].EventId, Is.EqualTo("DASH-2"));
    }

    [Test]
    public async Task DemographicTrigger_AgeBasedIntervention()
    {
        string patientId = $"DSI-FUNC-{Guid.NewGuid():N}";
        string intId = Guid.NewGuid().ToString("N");

        // Create patient over 65
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.UpdateDemographicsAsync("Test, Patient", "M", DateTime.UtcNow.AddYears(-70), "123-45-6789");

        // Register age-based screening intervention
        IDsiInterventionGrain intGrain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId}");
        await intGrain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId,
            Title = "Annual Wellness Visit Reminder",
            InterventionType = "evidence-based",
            IsActive = true,
            SourceCitation = "CMS Annual Wellness Visit",
            Severity = "info",
            RecommendedAction = "Schedule Annual Wellness Visit",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Demographic", ValueSetOrCode = "Age",
                    Operator = "greater-than", ComparisonValue = "65" }
            }
        });

        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId, Title = "Annual Wellness Visit Reminder",
            InterventionType = "evidence-based", IsActive = true, Severity = "info"
        });

        // Evaluate — should trigger for 70-year-old
        IDsiEvaluationGrain evalGrain = _cluster.GrainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
        List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

        Assert.That(results.Any(r => r.InterventionId == intId), Is.True);
        DsiEvaluationResult ageAlert = results.First(r => r.InterventionId == intId);
        Assert.That(ageAlert.TriggerEvidence.Any(e => e.Contains("Age:")), Is.True);
    }
}
