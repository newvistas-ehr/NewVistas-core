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
/// Unit tests for Decision Support Intervention (DSI) grains.
/// §170.315(b)(11) — Decision Support Interventions.
/// </summary>
[TestFixture]
public class DsiGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Intervention CRUD ────────────────────────────────────────────────────

    [Test]
    public async Task InterventionGrain_CanSaveAndRetrieve()
    {
        string id = Guid.NewGuid().ToString("N");
        IDsiInterventionGrain grain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{id}");

        var intervention = new DsiInterventionState
        {
            InterventionId = id,
            Title = "Sepsis Early Warning",
            Description = "Detects early signs of sepsis based on SIRS criteria",
            InterventionType = "evidence-based",
            ClinicalDomain = "diagnostic",
            IsActive = true,
            SourceCitation = "Surviving Sepsis Campaign 2021",
            Developer = "NewVistas Clinical Team",
            Severity = "critical",
            RecommendedAction = "Order blood cultures and start broad-spectrum antibiotics",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Vital", ValueSetOrCode = "Temperature", Operator = "greater-than", ComparisonValue = "38.3" },
                new() { DataSource = "Vital", ValueSetOrCode = "HeartRate", Operator = "greater-than", ComparisonValue = "90" }
            }
        };

        await grain.SaveInterventionAsync(intervention);
        DsiInterventionState result = await grain.GetInterventionAsync();

        Assert.That(result.Title, Is.EqualTo("Sepsis Early Warning"));
        Assert.That(result.InterventionType, Is.EqualTo("evidence-based"));
        Assert.That(result.Severity, Is.EqualTo("critical"));
        Assert.That(result.TriggerCriteria, Has.Count.EqualTo(2));
        Assert.That(result.SourceCitation, Is.EqualTo("Surviving Sepsis Campaign 2021"));
    }

    [Test]
    public async Task InterventionGrain_CanSetActive()
    {
        string id = Guid.NewGuid().ToString("N");
        IDsiInterventionGrain grain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{id}");

        await grain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = id,
            Title = "Test Alert",
            IsActive = true
        });

        await grain.SetActiveAsync(false);
        DsiInterventionState result = await grain.GetInterventionAsync();
        Assert.That(result.IsActive, Is.False);

        await grain.SetActiveAsync(true);
        result = await grain.GetInterventionAsync();
        Assert.That(result.IsActive, Is.True);
    }

    [Test]
    public async Task InterventionGrain_PredictiveDsi_StoresHti1Fields()
    {
        string id = Guid.NewGuid().ToString("N");
        IDsiInterventionGrain grain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{id}");

        await grain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = id,
            Title = "Sepsis Risk Predictor",
            InterventionType = "predictive",
            ModelPurpose = "Predict sepsis onset within 6 hours",
            Developer = "AI Health Labs",
            TrainingDataDescription = "50,000 ICU admissions 2018-2023",
            PerformanceMetrics = "AUROC 0.89, Sensitivity 0.85, Specificity 0.82",
            KnownLimitations = "Reduced accuracy in pediatric populations",
            FairnessAssessment = "Tested across age, sex, race — no significant disparities",
            InputDataRequirements = "Vitals (HR, Temp, BP, RR), WBC count, lactate",
            OutputDescription = "Probability score 0-1 with risk category (low/medium/high)"
        });

        DsiInterventionState result = await grain.GetInterventionAsync();
        Assert.That(result.InterventionType, Is.EqualTo("predictive"));
        Assert.That(result.ModelPurpose, Is.EqualTo("Predict sepsis onset within 6 hours"));
        Assert.That(result.PerformanceMetrics, Does.Contain("AUROC 0.89"));
        Assert.That(result.KnownLimitations, Does.Contain("pediatric"));
        Assert.That(result.FairnessAssessment, Does.Contain("no significant disparities"));
    }

    // ─── Intervention Index ───────────────────────────────────────────────────

    [Test]
    public async Task InterventionIndex_CanAddAndList()
    {
        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>(
            $"DSI-INDEX-{Guid.NewGuid():N}");

        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = "INT-001", Title = "Drug Interaction Alert",
            InterventionType = "evidence-based", ClinicalDomain = "medication",
            IsActive = true, Severity = "warning"
        });
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = "INT-002", Title = "Fall Risk Predictor",
            InterventionType = "predictive", ClinicalDomain = "preventive",
            IsActive = true, Severity = "info"
        });

        List<DsiInterventionSummary> all = await index.GetAllInterventionsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task InterventionIndex_FiltersByActiveStatus()
    {
        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>(
            $"DSI-INDEX-{Guid.NewGuid():N}");

        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = "INT-A", Title = "Active Alert", IsActive = true
        });
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = "INT-B", Title = "Inactive Alert", IsActive = false
        });

        List<DsiInterventionSummary> active = await index.GetActiveInterventionsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].InterventionId, Is.EqualTo("INT-A"));
    }

    [Test]
    public async Task InterventionIndex_CanRemove()
    {
        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>(
            $"DSI-INDEX-{Guid.NewGuid():N}");

        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = "INT-DEL", Title = "To Delete", IsActive = true
        });
        await index.RemoveInterventionAsync("INT-DEL");

        List<DsiInterventionSummary> all = await index.GetAllInterventionsAsync();
        Assert.That(all, Has.Count.EqualTo(0));
    }

    // ─── Event Recording ──────────────────────────────────────────────────────

    [Test]
    public async Task EventGrain_CanRecordFiring()
    {
        string eventId = $"DSI-EVENT:{Guid.NewGuid():N}";
        IDsiEventGrain grain = _cluster.GrainFactory.GetGrain<IDsiEventGrain>(eventId);

        await grain.RecordFiringAsync(
            "INT-001", "Drug Interaction Alert", "evidence-based",
            "PAT-123", "DR-SMITH", "Review drug combination",
            "warning", new List<string> { "Medication: Warfarin", "Medication: Aspirin" },
            "FDA Drug Interaction Database");

        DsiEventState result = await grain.GetEventAsync();
        Assert.That(result.InterventionId, Is.EqualTo("INT-001"));
        Assert.That(result.PatientId, Is.EqualTo("PAT-123"));
        Assert.That(result.UserResponse, Is.EqualTo("pending"));
        Assert.That(result.TriggerEvidence, Has.Count.EqualTo(2));
        Assert.That(result.SourceCitation, Is.EqualTo("FDA Drug Interaction Database"));
    }

    [Test]
    public async Task EventGrain_CanRecordAcceptedResponse()
    {
        string eventId = $"DSI-EVENT:{Guid.NewGuid():N}";
        IDsiEventGrain grain = _cluster.GrainFactory.GetGrain<IDsiEventGrain>(eventId);

        await grain.RecordFiringAsync(
            "INT-001", "Test Alert", "evidence-based",
            "PAT-456", null, "Take action", "warning",
            new List<string> { "Evidence 1" }, "Source");

        await grain.RecordResponseAsync("accepted", null);

        DsiEventState result = await grain.GetEventAsync();
        Assert.That(result.UserResponse, Is.EqualTo("accepted"));
        Assert.That(result.OverrideReason, Is.Null);
        Assert.That(result.ResponseDate, Is.Not.Null);
    }

    [Test]
    public async Task EventGrain_CanRecordOverriddenResponse()
    {
        string eventId = $"DSI-EVENT:{Guid.NewGuid():N}";
        IDsiEventGrain grain = _cluster.GrainFactory.GetGrain<IDsiEventGrain>(eventId);

        await grain.RecordFiringAsync(
            "INT-002", "High Alert", "evidence-based",
            "PAT-789", "DR-JONES", "Stop medication", "critical",
            new List<string> { "Lab: Creatinine = 5.2" }, "AKI Guidelines");

        await grain.RecordResponseAsync("overridden", "Patient already on dialysis — intervention not applicable");

        DsiEventState result = await grain.GetEventAsync();
        Assert.That(result.UserResponse, Is.EqualTo("overridden"));
        Assert.That(result.OverrideReason, Does.Contain("dialysis"));
    }

    // ─── Event Index ──────────────────────────────────────────────────────────

    [Test]
    public async Task EventIndex_CanFilterByPatient()
    {
        IDsiEventIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>(
            $"DSI-EVENT-INDEX-{Guid.NewGuid():N}");

        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-1", InterventionId = "INT-001",
            PatientId = "PAT-A", FiredDate = DateTime.UtcNow, UserResponse = "pending"
        });
        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-2", InterventionId = "INT-002",
            PatientId = "PAT-B", FiredDate = DateTime.UtcNow, UserResponse = "accepted"
        });
        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-3", InterventionId = "INT-001",
            PatientId = "PAT-A", FiredDate = DateTime.UtcNow, UserResponse = "overridden"
        });

        List<DsiEventSummary> patA = await index.GetEventsByPatientAsync("PAT-A");
        Assert.That(patA, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EventIndex_CanFilterByIntervention()
    {
        IDsiEventIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>(
            $"DSI-EVENT-INDEX-{Guid.NewGuid():N}");

        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-A", InterventionId = "INT-X",
            PatientId = "PAT-1", FiredDate = DateTime.UtcNow, UserResponse = "pending"
        });
        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-B", InterventionId = "INT-Y",
            PatientId = "PAT-2", FiredDate = DateTime.UtcNow, UserResponse = "accepted"
        });
        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-C", InterventionId = "INT-X",
            PatientId = "PAT-3", FiredDate = DateTime.UtcNow, UserResponse = "pending"
        });

        List<DsiEventSummary> intX = await index.GetEventsByInterventionAsync("INT-X");
        Assert.That(intX, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EventIndex_CanFilterPending()
    {
        IDsiEventIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>(
            $"DSI-EVENT-INDEX-{Guid.NewGuid():N}");

        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-P1", InterventionId = "INT-1",
            PatientId = "PAT-1", FiredDate = DateTime.UtcNow, UserResponse = "pending"
        });
        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-P2", InterventionId = "INT-1",
            PatientId = "PAT-2", FiredDate = DateTime.UtcNow, UserResponse = "accepted"
        });
        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-P3", InterventionId = "INT-2",
            PatientId = "PAT-3", FiredDate = DateTime.UtcNow, UserResponse = "pending"
        });

        List<DsiEventSummary> pending = await index.GetPendingEventsAsync();
        Assert.That(pending, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EventIndex_CanUpdateResponse()
    {
        IDsiEventIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiEventIndexGrain>(
            $"DSI-EVENT-INDEX-{Guid.NewGuid():N}");

        await index.AddEventAsync(new DsiEventSummary
        {
            EventId = "EVT-UPD", InterventionId = "INT-1",
            PatientId = "PAT-1", FiredDate = DateTime.UtcNow, UserResponse = "pending"
        });

        await index.UpdateResponseAsync("EVT-UPD", "accepted");

        List<DsiEventSummary> pending = await index.GetPendingEventsAsync();
        Assert.That(pending, Has.Count.EqualTo(0));

        List<DsiEventSummary> all = await index.GetAllEventsAsync();
        Assert.That(all[0].UserResponse, Is.EqualTo("accepted"));
    }

    // ─── Evaluation Engine ────────────────────────────────────────────────────

    [Test]
    public async Task EvaluationGrain_DetectsMatchingProblem()
    {
        string patientId = $"DSI-TEST-{Guid.NewGuid():N}";

        // Set up patient with a problem
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.AddProblemAsync("Type 2 Diabetes", "E11.9", "Active", "Chronic",
            DateTime.UtcNow.AddYears(-2), null, null, null, null, false, null);

        // Register an intervention that matches
        string intId = Guid.NewGuid().ToString("N");
        IDsiInterventionGrain intGrain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId}");
        await intGrain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId,
            Title = "Diabetes HbA1c Monitoring",
            InterventionType = "evidence-based",
            IsActive = true,
            SourceCitation = "ADA Standards of Care 2024",
            RecommendedAction = "Order HbA1c test if not done in past 3 months",
            Severity = "info",
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "E11.*", Operator = "exists" }
            }
        });

        // Register in index
        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId, Title = "Diabetes HbA1c Monitoring",
            InterventionType = "evidence-based", IsActive = true, Severity = "info"
        });

        // Evaluate
        IDsiEvaluationGrain evalGrain = _cluster.GrainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
        List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        DsiEvaluationResult match = results.First(r => r.InterventionId == intId);
        Assert.That(match.Title, Is.EqualTo("Diabetes HbA1c Monitoring"));
        Assert.That(match.SourceCitation, Is.EqualTo("ADA Standards of Care 2024"));
        Assert.That(match.TriggerEvidence.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task EvaluationGrain_InactiveInterventionNotTriggered()
    {
        string patientId = $"DSI-TEST-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.AddProblemAsync("Hypertension", "I10", "Active", "Chronic",
            DateTime.UtcNow, null, null, null, null, false, null);

        string intId = Guid.NewGuid().ToString("N");
        IDsiInterventionGrain intGrain = _cluster.GrainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intId}");
        await intGrain.SaveInterventionAsync(new DsiInterventionState
        {
            InterventionId = intId,
            Title = "HTN BP Monitoring",
            IsActive = false,
            TriggerCriteria = new List<DsiTriggerCriterion>
            {
                new() { DataSource = "Problem", ValueSetOrCode = "I10", Operator = "exists" }
            }
        });

        IDsiInterventionIndexGrain index = _cluster.GrainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        await index.AddInterventionAsync(new DsiInterventionSummary
        {
            InterventionId = intId, Title = "HTN BP Monitoring",
            IsActive = false
        });

        IDsiEvaluationGrain evalGrain = _cluster.GrainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
        List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

        Assert.That(results.Any(r => r.InterventionId == intId), Is.False);
    }
}
