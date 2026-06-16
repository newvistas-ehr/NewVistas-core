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
/// Functional tests for Electronic Case Reporting (eCR) workflow.
/// §170.315(f)(5) — Electronic Case Reporting.
///
/// Tests the full eCR lifecycle:
///   1. Register reportable condition triggers (RCTC)
///   2. Screen patients for reportable conditions
///   3. Generate eICR documents
///   4. Submit to public health and receive Reportability Response
///   5. Track case status through the index
/// </summary>
[TestFixture]
public class EcrWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Full eCR Workflow ───────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_RegisterTrigger_ScreenPatient_GenerateAndSubmitEicr()
    {
        // 1. Register reportable condition triggers
        string measlesTriggerId = $"MEASLES-{Guid.NewGuid():N}";
        IEcrTriggerGrain measlesTrigger = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{measlesTriggerId}");
        await measlesTrigger.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = measlesTriggerId,
            ConditionName = "Measles",
            ConditionCode = "14189004",
            ConditionCodeSystem = "SNOMED",
            Category = "communicable",
            ReportingTimeframe = "Immediately",
            IsActive = true,
            Jurisdictions = new List<string> { "US", "Virginia" },
            TriggerCodes = new List<EcrTriggerCode>
            {
                new() { Code = "B05.*", CodeSystem = "ICD-10", Description = "Measles (all subtypes)", TriggerType = "diagnosis" }
            }
        });

        IEcrTriggerIndexGrain triggerIndex = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
        await triggerIndex.AddTriggerAsync(new EcrTriggerSummary
        {
            TriggerId = measlesTriggerId, ConditionName = "Measles", IsActive = true,
            Category = "communicable", ReportingTimeframe = "Immediately", TriggerCodeCount = 1
        });

        // 2. Create a patient with measles
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("MEASLES,JOHN", "M", DateTime.UtcNow.AddYears(-28), "123-45-6789");
        await w.AddProblemAsync("Measles with intestinal complications", "B05.4", "active",
            null, null, null, null, null, null, false, null);

        // 3. Screen for reportable conditions
        IEcrScreeningGrain screening = _cluster.GrainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
        List<EcrScreeningMatch> matches = await screening.ScreenPatientAsync();

        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].ConditionName, Is.EqualTo("Measles"));
        Assert.That(matches[0].Jurisdictions, Contains.Item("US"));

        // 4. Create case report from the match
        EcrScreeningMatch match = matches[0];
        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain caseGrain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);
        await caseGrain.CreateCaseAsync(
            patientId, match.TriggerId, match.ConditionName,
            match.MatchedCode, match.MatchedCodeSystem, match.MatchedDescription,
            match.Jurisdictions, match.ClinicalEvidence,
            "DR-SMITH", "VA Medical Center");

        // Add to index
        IEcrCaseIndexGrain caseIndex = _cluster.GrainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
        EcrCaseState caseState = await caseGrain.GetCaseAsync();
        await caseIndex.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = caseId, PatientId = patientId, PatientName = caseState.PatientName,
            ConditionName = "Measles", Status = "triggered", TriggeredDate = caseState.TriggeredDate
        });

        Assert.That(caseState.Status, Is.EqualTo("triggered"));
        Assert.That(caseState.PatientName, Does.Contain("MEASLES"));

        // 5. Generate eICR document
        await caseGrain.GenerateEicrAsync();
        caseState = await caseGrain.GetCaseAsync();
        Assert.That(caseState.Status, Is.EqualTo("generated"));
        Assert.That(caseState.EicrDocument, Does.Contain("ClinicalDocument"));
        Assert.That(caseState.EicrDocument, Does.Contain("2.16.840.1.113883.10.20.15.2")); // eICR template
        Assert.That(caseState.EicrDocument, Does.Contain("Measles"));
        Assert.That(caseState.EicrDocument, Does.Contain("VA Medical Center"));

        // 6. Submit to public health
        await caseGrain.MarkSubmittedAsync();
        caseState = await caseGrain.GetCaseAsync();
        Assert.That(caseState.Status, Is.EqualTo("submitted"));
        Assert.That(caseState.SubmittedDate, Is.Not.Null);

        // 7. Receive Reportability Response
        await caseGrain.RecordReportabilityResponseAsync(
            "reportable",
            "Measles is reportable in Virginia. Immediate notification required to VDH.");
        caseState = await caseGrain.GetCaseAsync();
        Assert.That(caseState.Status, Is.EqualTo("reportable"));
        Assert.That(caseState.ReportabilityDetermination, Is.EqualTo("reportable"));

        // 8. Verify index reflects status changes
        await caseIndex.UpdateCaseStatusAsync(caseId, "reportable", "reportable");
        List<EcrCaseSummary> allCases = await caseIndex.GetAllCasesAsync();
        Assert.That(allCases.Any(c => c.CaseId == caseId && c.Status == "reportable"), Is.True);
    }

    // ─── Multiple Triggers ───────────────────────────────────────────────────

    [Test]
    public async Task MultipleTriggers_PatientMatchesMultipleConditions()
    {
        // Register two triggers
        string tbTriggerId = $"TB-{Guid.NewGuid():N}";
        IEcrTriggerGrain tbTrigger = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{tbTriggerId}");
        await tbTrigger.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = tbTriggerId, ConditionName = "Tuberculosis", IsActive = true,
            Category = "communicable", ReportingTimeframe = "24 hours",
            Jurisdictions = new List<string> { "US" },
            TriggerCodes = new List<EcrTriggerCode>
            {
                new() { Code = "A15.*", CodeSystem = "ICD-10", Description = "Respiratory TB", TriggerType = "diagnosis" }
            }
        });

        string hepTriggerId = $"HEP-{Guid.NewGuid():N}";
        IEcrTriggerGrain hepTrigger = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{hepTriggerId}");
        await hepTrigger.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = hepTriggerId, ConditionName = "Hepatitis B", IsActive = true,
            Category = "communicable", ReportingTimeframe = "24 hours",
            Jurisdictions = new List<string> { "US" },
            TriggerCodes = new List<EcrTriggerCode>
            {
                new() { Code = "B16.*", CodeSystem = "ICD-10", Description = "Acute hepatitis B", TriggerType = "diagnosis" }
            }
        });

        IEcrTriggerIndexGrain triggerIndex = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
        await triggerIndex.AddTriggerAsync(new EcrTriggerSummary { TriggerId = tbTriggerId, ConditionName = "Tuberculosis", IsActive = true });
        await triggerIndex.AddTriggerAsync(new EcrTriggerSummary { TriggerId = hepTriggerId, ConditionName = "Hepatitis B", IsActive = true });

        // Patient with both TB and Hep B
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("MULTI,CONDITION", "F", DateTime.UtcNow.AddYears(-55), "000-00-0020");
        await w.AddProblemAsync("Tuberculosis of lung", "A15.0", "active",
            null, null, null, null, null, null, false, null);
        await w.AddProblemAsync("Acute hepatitis B with delta-agent", "B16.0", "active",
            null, null, null, null, null, null, false, null);

        IEcrScreeningGrain screening = _cluster.GrainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
        List<EcrScreeningMatch> matches = await screening.ScreenPatientAsync();

        Assert.That(matches, Has.Count.EqualTo(2));
        Assert.That(matches.Any(m => m.ConditionName == "Tuberculosis"), Is.True);
        Assert.That(matches.Any(m => m.ConditionName == "Hepatitis B"), Is.True);
    }

    // ─── Inactive Trigger ────────────────────────────────────────────────────

    [Test]
    public async Task InactiveTrigger_NotDetectedInScreening()
    {
        string triggerId = $"INACTIVE-{Guid.NewGuid():N}";
        IEcrTriggerGrain triggerGrain = _cluster.GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");
        await triggerGrain.SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId, ConditionName = "Inactive Condition", IsActive = false,
            TriggerCodes = new List<EcrTriggerCode>
            {
                new() { Code = "Z99.*", CodeSystem = "ICD-10", Description = "Test", TriggerType = "diagnosis" }
            }
        });

        IEcrTriggerIndexGrain triggerIndex = _cluster.GrainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
        await triggerIndex.AddTriggerAsync(new EcrTriggerSummary
        {
            TriggerId = triggerId, ConditionName = "Inactive Condition", IsActive = false
        });

        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("INACTIVE,TEST", "M", DateTime.UtcNow.AddYears(-40), "000-00-0030");
        await w.AddProblemAsync("Dependence on other enabling machines", "Z99.8", "active",
            null, null, null, null, null, null, false, null);

        IEcrScreeningGrain screening = _cluster.GrainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
        List<EcrScreeningMatch> matches = await screening.ScreenPatientAsync();

        // Active triggers from previous tests may match, but the inactive one should not
        bool inactiveMatch = matches.Any(m => m.ConditionName == "Inactive Condition");
        Assert.That(inactiveMatch, Is.False);
    }

    // ─── Not-Reportable Response ─────────────────────────────────────────────

    [Test]
    public async Task ReportabilityResponse_NotReportable()
    {
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("NOTREP,PATIENT", "F", DateTime.UtcNow.AddYears(-30), "000-00-0040");

        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain caseGrain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);
        await caseGrain.CreateCaseAsync(patientId, "T-NR", "Test Condition", "X00", "ICD-10",
            "Test condition", new List<string> { "US" }, new List<string>(), null, null);
        await caseGrain.GenerateEicrAsync();
        await caseGrain.MarkSubmittedAsync();
        await caseGrain.RecordReportabilityResponseAsync("not-reportable",
            "This condition is not reportable in this jurisdiction.");

        EcrCaseState result = await caseGrain.GetCaseAsync();
        Assert.That(result.Status, Is.EqualTo("not-reportable"));
        Assert.That(result.ReportabilityDetermination, Is.EqualTo("not-reportable"));
    }

    // ─── Case Index Dashboard Queries ────────────────────────────────────────

    [Test]
    public async Task CaseIndex_DashboardQueries()
    {
        string indexKey = $"ECR-CASE-INDEX-FT-{Guid.NewGuid():N}";
        IEcrCaseIndexGrain index = _cluster.GrainFactory.GetGrain<IEcrCaseIndexGrain>(indexKey);

        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "FT-1", PatientId = "P-100", PatientName = "Patient A",
            ConditionName = "Measles", Status = "submitted", TriggeredDate = DateTime.UtcNow.AddDays(-2)
        });
        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "FT-2", PatientId = "P-200", PatientName = "Patient B",
            ConditionName = "TB", Status = "reportable", TriggeredDate = DateTime.UtcNow.AddDays(-1),
            ReportabilityDetermination = "reportable"
        });
        await index.AddCaseAsync(new EcrCaseSummary
        {
            CaseId = "FT-3", PatientId = "P-100", PatientName = "Patient A",
            ConditionName = "COVID-19", Status = "submitted", TriggeredDate = DateTime.UtcNow
        });

        // All cases
        Assert.That((await index.GetAllCasesAsync()), Has.Count.EqualTo(3));

        // By status
        Assert.That((await index.GetCasesByStatusAsync("submitted")), Has.Count.EqualTo(2));
        Assert.That((await index.GetCasesByStatusAsync("reportable")), Has.Count.EqualTo(1));

        // By patient
        Assert.That((await index.GetCasesByPatientAsync("P-100")), Has.Count.EqualTo(2));
        Assert.That((await index.GetCasesByPatientAsync("P-200")), Has.Count.EqualTo(1));

        // By condition
        Assert.That((await index.GetCasesByConditionAsync("Measles")), Has.Count.EqualTo(1));
    }

    // ─── eICR Document Content ───────────────────────────────────────────────

    [Test]
    public async Task EicrDocument_ContainsRequiredElements()
    {
        string patientId = $"PATIENT-ECR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("EICR,DETAIL", "M", new DateTime(1985, 6, 15), "000-00-0050");

        string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
        IEcrCaseGrain caseGrain = _cluster.GrainFactory.GetGrain<IEcrCaseGrain>(caseId);
        await caseGrain.CreateCaseAsync(
            patientId, "T-DETAIL", "Pertussis", "A37.0", "ICD-10",
            "Whooping cough due to Bordetella pertussis",
            new List<string> { "US", "Maryland" },
            new List<string> { "Diagnosis: Pertussis (A37.0)", "Cough duration: 3 weeks" },
            "DR-JONES", "VA Baltimore");

        await caseGrain.GenerateEicrAsync();
        EcrCaseState ecrCase = await caseGrain.GetCaseAsync();
        string xml = ecrCase.EicrDocument!;

        // Required eICR elements
        Assert.That(xml, Does.Contain("ClinicalDocument"));
        Assert.That(xml, Does.Contain("2.16.840.1.113883.10.20.15.2")); // eICR template
        Assert.That(xml, Does.Contain("2.16.840.1.113883.10.20.22.1.1")); // US Realm Header
        Assert.That(xml, Does.Contain("55751-2")); // Public Health Case Report LOINC
        Assert.That(xml, Does.Contain("recordTarget")); // Patient
        Assert.That(xml, Does.Contain(patientId)); // Patient ID
        Assert.That(xml, Does.Contain("EICR")); // Patient name
        Assert.That(xml, Does.Contain("19850615")); // DOB
        Assert.That(xml, Does.Contain("Pertussis")); // Condition
        Assert.That(xml, Does.Contain("A37.0")); // ICD-10 code
        Assert.That(xml, Does.Contain("VA Baltimore")); // Facility
        Assert.That(xml, Does.Contain("Maryland")); // Jurisdiction
        Assert.That(xml, Does.Contain("Cough duration")); // Clinical evidence
    }
}
