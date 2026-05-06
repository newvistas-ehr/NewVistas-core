// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Surgery — File #130.
/// Tests Surgery grain methods directly for scheduling, pre-op, intra-op,
/// complications, implants, specimens, and full lifecycle workflows.
/// </summary>
[TestFixture]
public class SurgeryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ISurgeryGrain NewSurgeryGrain()
        => _cluster.GrainFactory.GetGrain<ISurgeryGrain>($"SURG-{Guid.NewGuid():N}");

    private async Task ScheduleStandardAsync(ISurgeryGrain grain, string procedure = "APPENDECTOMY", string patientId = "PAT-001")
    {
        await grain.ScheduleSurgeryAsync(
            patientId, procedure, "44950",
            DateTime.UtcNow.AddDays(7),
            "SURG-001", "Dr. Thompson",
            "GENERAL", "GENERAL SURGERY",
            "Acute appendicitis",
            "LOC-OR-01", "Operating Room 3",
            "Patient consented, pre-op labs complete");
    }

    // ─── 1. Schedule ─────────────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanSchedule()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();

        // Act
        await ScheduleStandardAsync(grain);
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(state.PrincipalProcedure, Is.EqualTo("APPENDECTOMY"));
        Assert.That(state.PrincipalProcedureCptCode, Is.EqualTo("44950"));
        Assert.That(state.SurgeonName, Is.EqualTo("Dr. Thompson"));
        Assert.That(state.AnesthesiaTechnique, Is.EqualTo("GENERAL"));
        Assert.That(state.SurgicalSpecialty, Is.EqualTo("GENERAL SURGERY"));
        Assert.That(state.PreOpDiagnosis, Is.EqualTo("Acute appendicitis"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
    }

    // ─── 2. Complete ─────────────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanComplete()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.CompleteAsync();
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
    }

    // ─── 3. Cancel ───────────────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanCancel()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.CancelAsync("Patient developed URI");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Is.EqualTo("Patient developed URI"));
    }

    // ─── 4. Pre-Op Assessment ────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanRecordPreOpAssessment()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);
        DateTime assessmentDate = DateTime.UtcNow;

        // Act
        await grain.RecordPreOpAssessmentAsync(2, "Patient cleared for surgery", "PROV-001", "Dr. Smith", assessmentDate);
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.AsaClassification, Is.EqualTo(2));
        Assert.That(state.PreOpAssessmentNotes, Is.EqualTo("Patient cleared for surgery"));
        Assert.That(state.PreOpAssessmentProviderId, Is.EqualTo("PROV-001"));
        Assert.That(state.PreOpAssessmentProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.PreOpAssessmentDate, Is.Not.Null);
    }

    // ─── 5. Add Complication ─────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddComplication()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);
        await grain.CompleteAsync();
        DateTime occurrenceDate = DateTime.UtcNow;

        // Act
        await grain.AddComplicationAsync("SSI-001", "Surgical site infection", "MINOR", occurrenceDate, "IV antibiotics");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Complications, Has.Count.EqualTo(1));
        Assert.That(state.Complications[0].ComplicationCode, Is.EqualTo("SSI-001"));
        Assert.That(state.Complications[0].Description, Is.EqualTo("Surgical site infection"));
        Assert.That(state.Complications[0].Severity, Is.EqualTo("MINOR"));
        Assert.That(state.Complications[0].TreatmentAction, Is.EqualTo("IV antibiotics"));
    }

    // ─── 6. Multiple Complications ──────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddMultipleComplications()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);
        await grain.CompleteAsync();

        // Act
        await grain.AddComplicationAsync("SSI-001", "Wound infection", "MINOR", DateTime.UtcNow, "Antibiotics");
        await grain.AddComplicationAsync("BLEED-001", "Post-op hemorrhage", "MAJOR", DateTime.UtcNow, "Return to OR");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Complications, Has.Count.EqualTo(2));
        Assert.That(state.Complications[0].ComplicationCode, Is.EqualTo("SSI-001"));
        Assert.That(state.Complications[1].ComplicationCode, Is.EqualTo("BLEED-001"));
    }

    // ─── 7. Add Implant ─────────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddImplant()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.AddImplantAsync("Total Knee Prosthesis", "Zimmer Biomet", "SN-12345", "LOT-67890", "Right Knee");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Implants, Has.Count.EqualTo(1));
        Assert.That(state.Implants[0].DeviceName, Is.EqualTo("Total Knee Prosthesis"));
        Assert.That(state.Implants[0].Manufacturer, Is.EqualTo("Zimmer Biomet"));
        Assert.That(state.Implants[0].SerialNumber, Is.EqualTo("SN-12345"));
        Assert.That(state.Implants[0].LotNumber, Is.EqualTo("LOT-67890"));
        Assert.That(state.Implants[0].BodySite, Is.EqualTo("Right Knee"));
    }

    // ─── 8. Add Surgical Assistant ──────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddSurgicalAssistant()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.AddSurgicalAssistantAsync("ASST-001", "Dr. Patel", "FIRST ASSISTANT");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Assistants, Has.Count.EqualTo(1));
        Assert.That(state.Assistants[0].AssistantId, Is.EqualTo("ASST-001"));
        Assert.That(state.Assistants[0].AssistantName, Is.EqualTo("Dr. Patel"));
        Assert.That(state.Assistants[0].Role, Is.EqualTo("FIRST ASSISTANT"));
    }

    // ─── 9. Record Intra-Op Details ─────────────────────────────────────────

    [Test]
    public async Task Surgery_CanRecordIntraOpDetails()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.RecordIntraOpDetailsAsync(250, 1, 1, 1, "PACU");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.EstimatedBloodLoss, Is.EqualTo(250));
        Assert.That(state.SpongeCountCorrect, Is.EqualTo(1));
        Assert.That(state.NeedleCountCorrect, Is.EqualTo(1));
        Assert.That(state.InstrumentCountCorrect, Is.EqualTo(1));
        Assert.That(state.DispositionAfterSurgery, Is.EqualTo("PACU"));
    }

    // ─── 10. Add Anesthesia Agent ───────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddAnesthesiaAgent()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.AddAnesthesiaAgentAsync("Propofol");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.AnesthesiaAgents, Has.Count.EqualTo(1));
        Assert.That(state.AnesthesiaAgents, Contains.Item("Propofol"));
    }

    // ─── 11. Multiple Anesthesia Agents ─────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddMultipleAnesthesiaAgents()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.AddAnesthesiaAgentAsync("Propofol");
        await grain.AddAnesthesiaAgentAsync("Sevoflurane");
        await grain.AddAnesthesiaAgentAsync("Fentanyl");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.AnesthesiaAgents, Has.Count.EqualTo(3));
        Assert.That(state.AnesthesiaAgents, Contains.Item("Propofol"));
        Assert.That(state.AnesthesiaAgents, Contains.Item("Sevoflurane"));
        Assert.That(state.AnesthesiaAgents, Contains.Item("Fentanyl"));
    }

    // ─── 12. Add Specimen ───────────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddSpecimen()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);
        DateTime collectionTime = DateTime.UtcNow;

        // Act
        await grain.AddSpecimenAsync("Appendix", "Right lower quadrant", "PATH-2026-001", collectionTime);
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.Specimens, Has.Count.EqualTo(1));
        Assert.That(state.Specimens[0].SpecimenType, Is.EqualTo("Appendix"));
        Assert.That(state.Specimens[0].BodySite, Is.EqualTo("Right lower quadrant"));
        Assert.That(state.Specimens[0].PathologyAccessionNumber, Is.EqualTo("PATH-2026-001"));
    }

    // ─── 13. Add Blood Product ──────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanAddBloodProduct()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act
        await grain.AddBloodProductAsync("2 UNITS PRBC");
        SurgeryState state = await grain.GetSurgeryAsync();

        // Assert
        Assert.That(state.BloodProductsGiven, Has.Count.EqualTo(1));
        Assert.That(state.BloodProductsGiven, Contains.Item("2 UNITS PRBC"));
    }

    // ─── 14. Get Complications ──────────────────────────────────────────────

    [Test]
    public async Task Surgery_CanGetComplications()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);
        await grain.CompleteAsync();
        await grain.AddComplicationAsync("DVT-001", "Deep vein thrombosis", "MAJOR", DateTime.UtcNow, "Anticoagulation");
        await grain.AddComplicationAsync("NAUSEA-001", "Post-op nausea", "MINOR", DateTime.UtcNow, "Ondansetron");

        // Act
        List<SurgicalComplication> complications = await grain.GetComplicationsAsync();

        // Assert
        Assert.That(complications, Has.Count.EqualTo(2));
        Assert.That(complications[0].Description, Is.EqualTo("Deep vein thrombosis"));
        Assert.That(complications[1].Description, Is.EqualTo("Post-op nausea"));
    }

    // ─── 15. Full Workflow ──────────────────────────────────────────────────

    [Test]
    public async Task Surgery_FullWorkflow()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();

        // Schedule
        await grain.ScheduleSurgeryAsync(
            "PAT-FULL", "RIGHT TOTAL KNEE ARTHROPLASTY", "27447",
            DateTime.UtcNow.AddDays(7), "SURG-010", "Dr. Martinez",
            "GENERAL", "ORTHOPEDICS", "Severe right knee osteoarthritis",
            "LOC-OR-01", "Operating Room 3", null);
        SurgeryState s1 = await grain.GetSurgeryAsync();
        Assert.That(s1.Status, Is.EqualTo("SCHEDULED"));

        // Pre-op assessment
        await grain.RecordPreOpAssessmentAsync(2, "Cleared for surgery", "PROV-010", "Dr. Lee", DateTime.UtcNow);
        SurgeryState s2 = await grain.GetSurgeryAsync();
        Assert.That(s2.AsaClassification, Is.EqualTo(2));

        // Add assistants and agents
        await grain.AddSurgicalAssistantAsync("ASST-010", "Dr. Jones", "FIRST ASSISTANT");
        await grain.AddAnesthesiaAgentAsync("Propofol");
        await grain.AddAnesthesiaAgentAsync("Rocuronium");

        // Intra-op
        await grain.RecordIntraOpDetailsAsync(300, 1, 1, 1, "PACU");

        // Complete
        await grain.RecordOperativeReportAsync(
            "TKA completed without complications. Tourniquet time 85 minutes.",
            "Tricompartmental osteoarthritis", "CLEAN");
        await grain.CompleteAsync();

        // Post-op complications and implants
        await grain.AddComplicationAsync("SSI-001", "Superficial wound infection", "MINOR", DateTime.UtcNow, "Oral antibiotics");
        await grain.AddImplantAsync("Knee Prosthesis", "Stryker", "SN-99999", "LOT-88888", "Right Knee");
        await grain.AddSpecimenAsync("Synovial tissue", "Right knee", "PATH-2026-050", DateTime.UtcNow);

        // Final assertions
        SurgeryState final_state = await grain.GetSurgeryAsync();
        Assert.That(final_state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(final_state.OperativeReport, Does.Contain("TKA completed"));
        Assert.That(final_state.PostOpDiagnosis, Is.EqualTo("Tricompartmental osteoarthritis"));
        Assert.That(final_state.WoundClassification, Is.EqualTo("CLEAN"));
        Assert.That(final_state.Complications, Has.Count.EqualTo(1));
        Assert.That(final_state.Implants, Has.Count.EqualTo(1));
        Assert.That(final_state.Specimens, Has.Count.EqualTo(1));
        Assert.That(final_state.AnesthesiaAgents, Has.Count.EqualTo(2));
        Assert.That(final_state.Assistants, Has.Count.EqualTo(1));
        Assert.That(final_state.EstimatedBloodLoss, Is.EqualTo(300));
        Assert.That(final_state.DispositionAfterSurgery, Is.EqualTo("PACU"));
    }

    // ─── 16. Intra-Op Counts Recorded ───────────────────────────────────────

    [Test]
    public async Task Surgery_IntraOpCountsRecorded()
    {
        // Arrange
        ISurgeryGrain grain = NewSurgeryGrain();
        await ScheduleStandardAsync(grain);

        // Act — all counts correct
        await grain.RecordIntraOpDetailsAsync(150, 1, 1, 1, "WARD");
        SurgeryState state1 = await grain.GetSurgeryAsync();

        Assert.That(state1.SpongeCountCorrect, Is.EqualTo(1));
        Assert.That(state1.NeedleCountCorrect, Is.EqualTo(1));
        Assert.That(state1.InstrumentCountCorrect, Is.EqualTo(1));
        Assert.That(state1.DispositionAfterSurgery, Is.EqualTo("WARD"));

        // Act — update with incorrect sponge count
        await grain.RecordIntraOpDetailsAsync(150, 0, 1, 1, "OR");
        SurgeryState state2 = await grain.GetSurgeryAsync();

        Assert.That(state2.SpongeCountCorrect, Is.EqualTo(0));
        Assert.That(state2.NeedleCountCorrect, Is.EqualTo(1));
        Assert.That(state2.InstrumentCountCorrect, Is.EqualTo(1));
        Assert.That(state2.DispositionAfterSurgery, Is.EqualTo("OR"));
    }
}
