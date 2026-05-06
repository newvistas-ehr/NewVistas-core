// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for surgery depth methods through IPatientWorkflowGrain.
/// Covers pre-op assessment, complications, implants, specimens, and full
/// surgical lifecycle — all via the workflow orchestration layer.
/// </summary>
[TestFixture]
public class SurgeryDepthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> ScheduleSurgery(IPatientWorkflowGrain wf)
        => await wf.ScheduleSurgeryAsync(
            "Total Knee Arthroplasty", "27447", DateTime.UtcNow.AddDays(14),
            "PROV-001", "Dr. Surgeon", "General",
            "Orthopedic Surgery", "Severe osteoarthritis right knee",
            "LOC-001", "OR-1", null);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task RecordPreOpAssessment_PersistsNotes()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        await wf.RecordPreOpAssessmentAsync(surgeryId, "Patient cleared for surgery. ASA class II.", "PROV-002", "Dr. Anesthesia");

        SurgeryState state = await wf.GetSurgeryAsync(surgeryId);
        Assert.That(state.PreOpAssessmentNotes, Is.EqualTo("Patient cleared for surgery. ASA class II."));
        Assert.That(state.PreOpAssessmentProviderId, Is.EqualTo("PROV-002"));
        Assert.That(state.PreOpAssessmentProviderName, Is.EqualTo("Dr. Anesthesia"));
        Assert.That(state.PreOpAssessmentDate, Is.Not.Null);
    }

    [Test]
    public async Task AddSurgicalComplication_PersistsComplication()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        await wf.CompleteSurgeryAsync(surgeryId, "Operative report text", "OA right knee");

        await wf.AddSurgicalComplicationAsync(surgeryId, "WOUND INFECTION", "Superficial wound infection at incision site", "MINOR", "Oral antibiotics");

        List<SurgicalComplication> complications = await wf.GetSurgicalComplicationsAsync(surgeryId);
        Assert.That(complications, Has.Count.EqualTo(1));
        Assert.That(complications[0].ComplicationCode, Is.EqualTo("WOUND INFECTION"));
        Assert.That(complications[0].Description, Is.EqualTo("Superficial wound infection at incision site"));
        Assert.That(complications[0].Severity, Is.EqualTo("MINOR"));
    }

    [Test]
    public async Task AddSurgicalImplant_PersistsImplantInfo()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        await wf.CompleteSurgeryAsync(surgeryId, "Operative report text", "OA right knee");

        await wf.AddSurgicalImplantAsync(surgeryId, "Total Knee System - Zimmer NexGen", "Zimmer Biomet", "SN-12345", "LOT-67890");

        SurgeryState state = await wf.GetSurgeryAsync(surgeryId);
        Assert.That(state.Implants, Has.Count.EqualTo(1));
        Assert.That(state.Implants[0].DeviceName, Is.EqualTo("Total Knee System - Zimmer NexGen"));
        Assert.That(state.Implants[0].Manufacturer, Is.EqualTo("Zimmer Biomet"));
        Assert.That(state.Implants[0].SerialNumber, Is.EqualTo("SN-12345"));
        Assert.That(state.Implants[0].LotNumber, Is.EqualTo("LOT-67890"));
    }

    [Test]
    public async Task AddSurgicalSpecimen_PersistsSpecimenInfo()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        await wf.CompleteSurgeryAsync(surgeryId, "Operative report text", "OA right knee");

        await wf.AddSurgicalSpecimenAsync(surgeryId, "Synovial tissue", "Right knee synovium", null);

        SurgeryState state = await wf.GetSurgeryAsync(surgeryId);
        Assert.That(state.Specimens, Has.Count.EqualTo(1));
        Assert.That(state.Specimens[0].SpecimenType, Is.EqualTo("Synovial tissue"));
    }

    [Test]
    public async Task GetSurgicalComplications_MultipleComplications()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        await wf.CompleteSurgeryAsync(surgeryId, "Operative report text", "OA right knee");

        await wf.AddSurgicalComplicationAsync(surgeryId, "WOUND INFECTION", "Superficial infection", "MINOR", "Antibiotics");
        await wf.AddSurgicalComplicationAsync(surgeryId, "DVT", "Deep vein thrombosis left calf", "MAJOR", "Anticoagulation therapy");

        List<SurgicalComplication> complications = await wf.GetSurgicalComplicationsAsync(surgeryId);
        Assert.That(complications, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AddMultipleImplants_AllAppear()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        await wf.CompleteSurgeryAsync(surgeryId, "Operative report text", "OA right knee");

        await wf.AddSurgicalImplantAsync(surgeryId, "Femoral Component", "Zimmer Biomet", "SN-001", "LOT-001");
        await wf.AddSurgicalImplantAsync(surgeryId, "Tibial Component", "Zimmer Biomet", "SN-002", "LOT-002");

        SurgeryState state = await wf.GetSurgeryAsync(surgeryId);
        Assert.That(state.Implants, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FullSurgicalWorkflow_PreOpThroughComplications()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string surgeryId = await ScheduleSurgery(wf);

        // Pre-op
        await wf.RecordPreOpAssessmentAsync(surgeryId, "Cleared. ASA II.", "PROV-002", "Dr. Anesthesia");

        // Complete
        await wf.CompleteSurgeryAsync(surgeryId, "Procedure completed without incident", "OA right knee");

        // Add complication
        await wf.AddSurgicalComplicationAsync(surgeryId, "HEMATOMA", "Small hematoma at surgical site", "MINOR", "Observation");

        // Add implant
        await wf.AddSurgicalImplantAsync(surgeryId, "Total Knee System", "Zimmer Biomet", "SN-999", "LOT-999");

        // Add specimen
        await wf.AddSurgicalSpecimenAsync(surgeryId, "Cartilage fragment", "Right knee", null);

        // Verify all
        SurgeryState state = await wf.GetSurgeryAsync(surgeryId);
        Assert.That(state.PreOpAssessmentNotes, Is.EqualTo("Cleared. ASA II."));
        Assert.That(state.Complications, Has.Count.EqualTo(1));
        Assert.That(state.Implants, Has.Count.EqualTo(1));
        Assert.That(state.Specimens, Has.Count.EqualTo(1));
    }
}
