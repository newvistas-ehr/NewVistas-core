// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Radiology Tech workflows — Exam Tracking, Protocols,
/// Worklist, Image Library linking (VistA RA — RARTE.m).
/// </summary>
[TestFixture]
public class RadTechWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IRadExamTrackingGrain NewExam()
        => _cluster.GrainFactory.GetGrain<IRadExamTrackingGrain>($"RAD-EXAM:{Guid.NewGuid():N}");
    private IRadWorklistGrain NewWorklist()
        => _cluster.GrainFactory.GetGrain<IRadWorklistGrain>($"RAD-WORKLIST:{Guid.NewGuid()}");
    private IRadProtocolGrain NewProtocol()
        => _cluster.GrainFactory.GetGrain<IRadProtocolGrain>($"RAD-PROTOCOL:{Guid.NewGuid()}");
    private IRadProtocolIndexGrain ProtocolIndex()
        => _cluster.GrainFactory.GetGrain<IRadProtocolIndexGrain>("RAD-PROTOCOL-INDEX");
    private IPatientWorkflowGrain WF(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    // ─── Exam Tracking ───────────────────────────────────────────────────────

    [Test]
    public async Task ExamTracking_Initialize_SetsOrderedStatus()
    {
        IRadExamTrackingGrain exam = NewExam();
        await exam.InitializeAsync("RAD-001", "PAT-001");
        RadExamTrackingState state = await exam.GetAsync();
        Assert.That(state.ExamStatus, Is.EqualTo(RadExamStatus.Ordered));
    }

    [Test]
    public async Task ExamTracking_Schedule_SetsScheduledWithRoom()
    {
        IRadExamTrackingGrain exam = NewExam();
        await exam.InitializeAsync("RAD-002", "PAT-002");
        await exam.ScheduleAsync(DateTime.UtcNow.AddHours(2), "CT-Room-1");
        RadExamTrackingState state = await exam.GetAsync();
        Assert.That(state.ExamStatus, Is.EqualTo(RadExamStatus.Scheduled));
        Assert.That(state.Room, Is.EqualTo("CT-Room-1"));
    }

    [Test]
    public async Task ExamTracking_FullLifecycle_ProgressesThroughStatuses()
    {
        IRadExamTrackingGrain exam = NewExam();
        await exam.InitializeAsync("RAD-003", "PAT-003");
        await exam.ScheduleAsync(DateTime.UtcNow, "MRI-Room-2");
        await exam.AssignProtocolAsync("PROT-MRI-BRAIN", "MRI Brain W/WO", "T1, T2, FLAIR, DWI");
        await exam.MarkPatientPreppedAsync("IV access confirmed, contrast allergy checked (none), changed to gown");
        await exam.StartExamAsync();
        await exam.CompleteExamAsync(186, "Standard brain MRI, no motion artifact");
        await exam.SendImagesToPacsAsync();

        RadExamTrackingState state = await exam.GetAsync();
        Assert.That(state.ExamStatus, Is.EqualTo(RadExamStatus.ImagesSentToPacs));
        Assert.That(state.ProtocolName, Is.EqualTo("MRI Brain W/WO"));
        Assert.That(state.ImageCount, Is.EqualTo(186));
        Assert.That(state.PrepNotes, Does.Contain("contrast allergy"));
    }

    [Test]
    public async Task ExamTracking_AssignProtocol_StoresParameters()
    {
        IRadExamTrackingGrain exam = NewExam();
        await exam.InitializeAsync("RAD-004", "PAT-004");
        await exam.AssignProtocolAsync("PROT-CT-ABD", "CT Abd/Pelvis W Contrast", "120kVp, 300mA, 5mm slice, 60s delay");
        RadExamTrackingState state = await exam.GetAsync();
        Assert.That(state.ProtocolId, Is.EqualTo("PROT-CT-ABD"));
        Assert.That(state.ProtocolParameters, Does.Contain("120kVp"));
    }

    [Test]
    public async Task ExamTracking_LinkImage_AddsToList()
    {
        IRadExamTrackingGrain exam = NewExam();
        await exam.InitializeAsync("RAD-005", "PAT-005");
        await exam.LinkImageAsync("IMG-001");
        await exam.LinkImageAsync("IMG-002");
        await exam.LinkImageAsync("IMG-001"); // duplicate — should not add again
        RadExamTrackingState state = await exam.GetAsync();
        Assert.That(state.LinkedImageIds, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ExamTracking_Cancel_SetsCancelledStatus()
    {
        IRadExamTrackingGrain exam = NewExam();
        await exam.InitializeAsync("RAD-006", "PAT-006");
        await exam.CancelExamAsync();
        Assert.That((await exam.GetAsync()).ExamStatus, Is.EqualTo(RadExamStatus.Cancelled));
    }

    // ─── Protocol ────────────────────────────────────────────────────────────

    [Test]
    public async Task Protocol_Create_StoresFields()
    {
        IRadProtocolGrain proto = NewProtocol();
        await proto.CreateAsync("CT Chest PE Protocol", "CT SCAN", "CHEST", "High-res PE protocol", "120kVp, 100mA, 1.25mm, 18s bolus");
        RadProtocolState state = await proto.GetAsync();
        Assert.That(state.ProtocolName, Is.EqualTo("CT Chest PE Protocol"));
        Assert.That(state.ImagingType, Is.EqualTo("CT SCAN"));
        Assert.That(state.IsActive, Is.True);
    }

    [Test]
    public async Task Protocol_Deactivate_SetsInactive()
    {
        IRadProtocolGrain proto = NewProtocol();
        await proto.CreateAsync("Old Protocol", "MRI", null, null, null);
        await proto.DeactivateAsync();
        Assert.That((await proto.GetAsync()).IsActive, Is.False);
    }

    [Test]
    public async Task ProtocolIndex_SelfSeeds_DemoProtocols()
    {
        List<RadProtocolIndexEntry> protos = await ProtocolIndex().GetAllAsync();
        Assert.That(protos.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(protos.Any(p => p.ProtocolId == "PROT-CXR-PA"), Is.True);
        Assert.That(protos.Any(p => p.ProtocolId == "PROT-MRI-BRAIN"), Is.True);
    }

    [Test]
    public async Task ProtocolIndex_GetByImagingType_Filters()
    {
        List<RadProtocolIndexEntry> cts = await ProtocolIndex().GetByImagingTypeAsync("CT SCAN");
        Assert.That(cts.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(cts.All(p => p.ImagingType == "CT SCAN"), Is.True);
    }

    // ─── Worklist ────────────────────────────────────────────────────────────

    [Test]
    public async Task Worklist_AddItem_AppearsInList()
    {
        IRadWorklistGrain wl = NewWorklist();
        await wl.AddItemAsync(new RadWorklistItem { RadiologyId = "RAD-W1", PatientId = "P1", ProcedureName = "CXR", ExamStatus = RadExamStatus.Ordered, OrderDateTime = DateTime.UtcNow });
        RadWorklistState state = await wl.GetAsync();
        Assert.That(state.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Worklist_GetUrgent_FiltersSTATAndURGENT()
    {
        IRadWorklistGrain wl = NewWorklist();
        await wl.AddItemAsync(new RadWorklistItem { RadiologyId = "R1", ProcedureName = "CXR", Urgency = "ROUTINE", OrderDateTime = DateTime.UtcNow });
        await wl.AddItemAsync(new RadWorklistItem { RadiologyId = "R2", ProcedureName = "CT Head", Urgency = "STAT", OrderDateTime = DateTime.UtcNow });
        await wl.AddItemAsync(new RadWorklistItem { RadiologyId = "R3", ProcedureName = "MRI Knee", Urgency = "URGENT", OrderDateTime = DateTime.UtcNow });
        List<RadWorklistItem> urgent = await wl.GetUrgentAsync();
        Assert.That(urgent, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Worklist_GetByStatus_FiltersCorrectly()
    {
        IRadWorklistGrain wl = NewWorklist();
        await wl.AddItemAsync(new RadWorklistItem { RadiologyId = "R4", ProcedureName = "CXR", ExamStatus = RadExamStatus.Ordered, OrderDateTime = DateTime.UtcNow });
        await wl.AddItemAsync(new RadWorklistItem { RadiologyId = "R5", ProcedureName = "CT", ExamStatus = RadExamStatus.InProgress, OrderDateTime = DateTime.UtcNow });
        List<RadWorklistItem> ordered = await wl.GetByStatusAsync(RadExamStatus.Ordered);
        Assert.That(ordered, Has.Count.EqualTo(1));
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_InitAndTrackExam_FullCycle()
    {
        string pid = $"PAT-RAD-WF-{Guid.NewGuid()}";
        // Order a rad study
        string radId = await WF(pid).OrderRadiologyStudyAsync(
            "71-001", "CT Head W/O Contrast", "70450", "CT SCAN",
            "PROV-1", "Dr. Smith", "STAT", "H/O CVA", "Rule out acute hemorrhage",
            null, "RAD-1", "Radiology");

        // Initialize tracking
        await WF(pid).InitializeRadExamTrackingAsync(radId, DateTime.UtcNow.AddMinutes(30), "CT-1");
        await WF(pid).AssignRadProtocolAsync(radId, "PROT-CT-HEAD", "CT Head W/O", "120kVp, 200mA, 5mm");
        await WF(pid).MarkRadPatientPreppedAsync(radId, "No allergies, IV not needed for non-contrast");
        await WF(pid).StartRadExamAsync(radId);
        await WF(pid).CompleteRadExamAsync(radId, 42, "Clean scan, no motion");
        await WF(pid).SendRadImagesToPacsAsync(radId);

        RadExamTrackingState state = await WF(pid).GetRadExamTrackingAsync(radId);
        Assert.That(state.ExamStatus, Is.EqualTo(RadExamStatus.ImagesSentToPacs));
        Assert.That(state.ImageCount, Is.EqualTo(42));
    }

    [Test]
    public async Task Workflow_LinkImageToExam_AddsImageId()
    {
        string pid = $"PAT-RAD-IMG-{Guid.NewGuid()}";
        string radId = await WF(pid).OrderRadiologyStudyAsync(
            "71-002", "Chest X-Ray", "71046", "GENERAL RADIOLOGY",
            null, null, "ROUTINE", null, null, null, null, null);
        await WF(pid).InitializeRadExamTrackingAsync(radId, null, null);
        await WF(pid).LinkImageToRadExamAsync(radId, "IMG-CXR-001");
        RadExamTrackingState state = await WF(pid).GetRadExamTrackingAsync(radId);
        Assert.That(state.LinkedImageIds, Does.Contain("IMG-CXR-001"));
    }

    [Test]
    public async Task Workflow_GetProtocols_ReturnsList()
    {
        string pid = $"PAT-RAD-PROT-{Guid.NewGuid()}";
        List<RadProtocolIndexEntry> protos = await WF(pid).GetRadProtocolsAsync();
        Assert.That(protos.Count, Is.GreaterThanOrEqualTo(8));
    }

    [Test]
    public async Task Workflow_GetProtocolsByType_FiltersMRI()
    {
        string pid = $"PAT-RAD-PROT2-{Guid.NewGuid()}";
        List<RadProtocolIndexEntry> mris = await WF(pid).GetRadProtocolsByTypeAsync("MRI");
        Assert.That(mris, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(mris.All(p => p.ImagingType == "MRI"), Is.True);
    }

    [Test]
    public async Task Workflow_RefreshRadWorklist_ReturnsState()
    {
        string pid = $"PAT-RAD-WL-{Guid.NewGuid()}";
        await WF(pid).OrderRadiologyStudyAsync("71-003", "US Abdomen", "76700", "ULTRASOUND", null, null, "ROUTINE", null, null, null, null, null);
        RadWorklistState wl = await WF(pid).RefreshRadWorklistAsync("RAD-MAIN");
        Assert.That(wl, Is.Not.Null);
    }
}
