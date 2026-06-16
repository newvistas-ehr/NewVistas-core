// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class SubsystemWorkflowTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    // ─── Surgery ─────────────────────────────────────────────────────────

    [Test]
    public async Task Surgery_ScheduleAndComplete()
    {
        var w = NewWorkflow();
        var id = await w.ScheduleSurgeryAsync("Total Knee Replacement", "27447",
            DateTime.UtcNow.AddDays(7), "SURG-001", "Dr. Ortho", "GENERAL",
            "Orthopedics", "OA Right Knee", null, null, null);

        Assert.That(id, Does.StartWith("SURG-"));

        var state = await w.GetSurgeryAsync(id);
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(state.PrincipalProcedure, Is.EqualTo("Total Knee Replacement"));

        await w.CompleteSurgeryAsync(id, "Procedure completed without complication. Implant placed.", "OA Right Knee, post-op");
        state = await w.GetSurgeryAsync(id);
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.OperativeReport, Does.Contain("Implant placed"));
    }

    [Test]
    public async Task Surgery_CancelAndList()
    {
        var w = NewWorkflow();
        var id1 = await w.ScheduleSurgeryAsync("Appendectomy", "44970",
            DateTime.UtcNow.AddDays(1), null, null, null, "General Surgery", null, null, null, null);
        var id2 = await w.ScheduleSurgeryAsync("Cholecystectomy", "47562",
            DateTime.UtcNow.AddDays(3), null, null, null, "General Surgery", null, null, null, null);

        await w.CancelSurgeryAsync(id1, "Patient declined");
        var state = await w.GetSurgeryAsync(id1);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));

        var list = await w.GetSurgeriesAsync(50);
        Assert.That(list, Has.Count.EqualTo(2));
    }

    // ─── Radiology ───────────────────────────────────────────────────────

    [Test]
    public async Task Radiology_OrderAndComplete()
    {
        var w = NewWorkflow();
        var id = await w.OrderRadiologyStudyAsync("Chest X-Ray PA/LAT", null, "71046", "GENERAL RADIOLOGY",
            "PROV-001", "Dr. Smith", "ROUTINE", "Cough x 3 weeks", "R/O pneumonia",
            null, null, null);

        Assert.That(id, Does.StartWith("RAD-"));

        await w.CompleteRadiologyAsync(id,
            "No acute cardiopulmonary disease. Clear lungs bilaterally.",
            "Normal chest radiograph",
            "RAD-001", "Dr. Radiologist");

        var state = await w.GetRadiologyStudyAsync(id);
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.ReportText, Does.Contain("Clear lungs"));

        var list = await w.GetRadiologyStudiesAsync(50);
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].HasReport, Is.True);
    }

    // ─── BCMA ────────────────────────────────────────────────────────────

    [Test]
    public async Task Bcma_RecordAndList()
    {
        var w = NewWorkflow();
        var id = await w.RecordMedicationAdministrationAsync(
            "Metoprolol 25mg", "DRUG-001", "25mg", "PO",
            "GIVEN", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            "RN-001", "Nurse Johnson", null, "RX-001", null, null);

        Assert.That(id, Does.StartWith("BCMA-"));

        var list = await w.GetMedicationAdministrationsAsync(50);
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].DrugName, Is.EqualTo("Metoprolol 25mg"));
        Assert.That(list[0].ActionStatus, Is.EqualTo("GIVEN"));
    }

    // ─── Imaging ─────────────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CaptureAndList()
    {
        var w = NewWorkflow();
        var id = await w.CaptureImageAsync("XRAY", "Chest X-Ray", "RAD",
            "https://blob.storage/img001.dcm", "https://blob.storage/img001_thumb.jpg",
            null, null, DateTime.UtcNow, DateTime.UtcNow, 2,
            null, null, "TECH-001", "Tech Wilson", null, null, null);

        Assert.That(id, Does.StartWith("IMG-"));

        var list = await w.GetImagesAsync(50);
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ObjectType, Is.EqualTo("XRAY"));
        Assert.That(list[0].ImageCount, Is.EqualTo(2));
    }

    // ─── Clinical Reminders ──────────────────────────────────────────────

    [Test]
    public async Task Reminders_CreateAndComplete()
    {
        var w = NewWorkflow();
        var id = await w.CreateReminderAsync("Influenza Vaccine", null, "PREVENTIVE",
            "HIGH", "ANNUAL", DateTime.UtcNow.AddDays(30));

        Assert.That(id, Does.StartWith("RMND-"));

        var list = await w.GetRemindersAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ReminderName, Is.EqualTo("Influenza Vaccine"));

        await w.CompleteReminderAsync(id, "PROV-001", "Dr. Smith");
        list = await w.GetRemindersAsync();
        Assert.That(list[0].Status, Is.EqualTo("DONE"));
    }

    // ─── Immunizations ──────────────────────────────────────────────────

    [Test]
    public async Task Immunizations_RecordAndList()
    {
        var w = NewWorkflow();
        var id = await w.RecordImmunizationAsync("COVID-19 mRNA Vaccine", "213",
            DateTime.UtcNow, "Dose 1", "LOT-ABC123", "Pfizer",
            "RN-001", "Nurse Jones", "Left Deltoid", "IM", "0.3 mL",
            null, null, null);

        Assert.That(id, Does.StartWith("IMM-"));

        var list = await w.GetImmunizationsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ImmunizationName, Is.EqualTo("COVID-19 mRNA Vaccine"));
        Assert.That(list[0].Series, Is.EqualTo("Dose 1"));
    }

    // ─── Health Factors ──────────────────────────────────────────────────

    [Test]
    public async Task HealthFactors_RecordAndList()
    {
        var w = NewWorkflow();
        var id = await w.RecordHealthFactorAsync("CURRENT SMOKER", "TOBACCO",
            DateTime.UtcNow, "MODERATE", null, null, null, null, null, null);

        Assert.That(id, Does.StartWith("HF-"));

        var list = await w.GetHealthFactorsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].HealthFactorName, Is.EqualTo("CURRENT SMOKER"));
        Assert.That(list[0].Category, Is.EqualTo("TOBACCO"));
    }

    // ─── Mental Health ───────────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_RecordAndList()
    {
        var w = NewWorkflow();
        var responses = new Dictionary<string, string>
        {
            { "Q1", "Several days" }, { "Q2", "More than half the days" }
        };
        var id = await w.RecordMentalHealthScreenAsync("PHQ-2", DateTime.UtcNow,
            3m, "MILD", false, responses, "PROV-001", "Dr. Smith", null, null, null);

        Assert.That(id, Does.StartWith("MH-"));

        var list = await w.GetMentalHealthScreensAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].InstrumentName, Is.EqualTo("PHQ-2"));
        Assert.That(list[0].TotalScore, Is.EqualTo(3));
        Assert.That(list[0].IsPositiveScreen, Is.False);
    }

    // ─── Dietetics ───────────────────────────────────────────────────────

    [Test]
    public async Task Dietetics_CreateDiscontinueAndList()
    {
        var w = NewWorkflow();
        var id = await w.CreateDietOrderAsync("REGULAR", "Regular Diet",
            ["LOW SODIUM", "DIABETIC"], "REGULAR", "THIN", "2000",
            "No shellfish", DateTime.UtcNow, "PROV-001", "Dr. Smith", null);

        Assert.That(id, Does.StartWith("DIET-"));

        var list = await w.GetDietOrdersAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].DietType, Is.EqualTo("REGULAR"));
        Assert.That(list[0].Status, Is.EqualTo("ACTIVE"));

        await w.DiscontinueDietOrderAsync(id);
        list = await w.GetDietOrdersAsync();
        Assert.That(list[0].Status, Is.EqualTo("DISCONTINUED"));
    }

    // ─── Prosthetics ────────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_IssueAndList()
    {
        var w = NewWorkflow();
        var id = await w.IssueProstheticAsync("Manual Wheelchair", "K0001", "MOBILITY",
            DateTime.UtcNow, 1, 450.00m, "PROV-001", "Dr. Rehab",
            null, null, true, null);

        Assert.That(id, Does.StartWith("PROS-"));

        var list = await w.GetProstheticsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ItemDescription, Is.EqualTo("Manual Wheelchair"));
        Assert.That(list[0].IsServiceConnected, Is.True);
    }

    // ─── Means Test ──────────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_RecordAndList()
    {
        var w = NewWorkflow();
        var id = await w.RecordMeansTestAsync("ANNUAL", DateTime.UtcNow,
            45000m, 12000m, 2, "ELIGIBLE", "GROUP 5",
            "CLERK-001", "Clerk Adams", null);

        Assert.That(id, Does.StartWith("MT-"));

        var list = await w.GetMeansTestsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].TestType, Is.EqualTo("ANNUAL"));
        Assert.That(list[0].EligibilityStatus, Is.EqualTo("ELIGIBLE"));
        Assert.That(list[0].PriorityGroup, Is.EqualTo("GROUP 5"));
    }

    // ─── Service Connected Conditions ────────────────────────────────────

    [Test]
    public async Task ServiceConnected_RecordAndList()
    {
        var w = NewWorkflow();
        var id = await w.RecordServiceConnectedConditionAsync(
            "PTSD", "F43.10", 70, true, new DateTime(2020, 1, 1),
            null, "Combat related");

        Assert.That(id, Does.StartWith("SC-"));

        var list = await w.GetServiceConnectedConditionsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Condition, Is.EqualTo("PTSD"));
        Assert.That(list[0].DisabilityPercentage, Is.EqualTo(70));
        Assert.That(list[0].IsServiceConnected, Is.True);
    }

    // ─── ADT ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Adt_AdmitAndDischarge()
    {
        var w = NewWorkflow();
        var id = await w.RecordAdmissionAsync(DateTime.UtcNow,
            "WARD-3A", "Ward 3A", "301-A", "Internal Medicine",
            "PROV-001", "Dr. Attending", "Pneumonia", null);

        Assert.That(id, Does.StartWith("ADT-"));

        var list = await w.GetAdtMovementsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].MovementType, Is.EqualTo("ADMISSION"));
        Assert.That(list[0].Status, Is.EqualTo("ADMITTED"));

        await w.RecordDischargeAsync(id, DateTime.UtcNow.AddDays(3),
            "Pneumonia, resolved", "REGULAR", null);

        list = await w.GetAdtMovementsAsync();
        Assert.That(list[0].Status, Is.EqualTo("DISCHARGED"));
    }

    [Test]
    public async Task Adt_MultipleAdmissions()
    {
        var w = NewWorkflow();
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-30), "WARD-2B", "Ward 2B",
            "201-C", "Cardiology", null, null, "CHF exacerbation", null);
        var id2 = await w.RecordAdmissionAsync(DateTime.UtcNow, "WARD-ICU", "ICU",
            "ICU-1", "Critical Care", "PROV-002", "Dr. Intensivist", "Sepsis", null);

        var list = await w.GetAdtMovementsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
        // Most recent first
        Assert.That(list[0].WardLocationName, Is.EqualTo("ICU"));
    }
}
