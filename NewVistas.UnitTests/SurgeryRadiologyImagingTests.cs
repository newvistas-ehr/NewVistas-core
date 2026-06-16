// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Surgery (File #130), Radiology (File #75.1), and Imaging (File #2005) grains.
/// </summary>
[TestFixture]
public class SurgeryGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ISurgeryGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<ISurgeryGrain>($"SURG-{Guid.NewGuid()}");

    [Test]
    public async Task SurgeryGrain_ScheduleSurgery_PersistsAllFields()
    {
        ISurgeryGrain grain = NewGrain();
        DateTime opDate = new DateTime(2026, 4, 15, 8, 0, 0);

        await grain.ScheduleSurgeryAsync(
            "PATIENT-001", "Appendectomy", "44950",
            opDate, "SURG-001", "Dr. Chen",
            "GENERAL", "General Surgery",
            "Acute appendicitis", "LOC-OR1", "OR Suite 1", "Elective");

        SurgeryState state = await grain.GetSurgeryAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.PrincipalProcedure, Is.EqualTo("Appendectomy"));
        Assert.That(state.PrincipalProcedureCptCode, Is.EqualTo("44950"));
        Assert.That(state.SurgeonId, Is.EqualTo("SURG-001"));
        Assert.That(state.SurgeonName, Is.EqualTo("Dr. Chen"));
        Assert.That(state.AnesthesiaTechnique, Is.EqualTo("GENERAL"));
        Assert.That(state.SurgicalSpecialty, Is.EqualTo("General Surgery"));
        Assert.That(state.PreOpDiagnosis, Is.EqualTo("Acute appendicitis"));
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(state.DateOfOperation, Is.EqualTo(opDate));
    }

    [Test]
    public async Task SurgeryGrain_BeginOperation_SetsInProgressStatus()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-002", "Cholecystectomy", null,
            DateTime.UtcNow, null, null,
            "SPINAL", "General Surgery",
            "Gallstones", null, null, null);

        DateTime began = DateTime.UtcNow;
        await grain.BeginOperationAsync(began, "ANES-001", "Dr. Patel");

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.Status, Is.EqualTo("IN PROGRESS"));
        Assert.That(state.TimeOperationBegan, Is.EqualTo(began));
        Assert.That(state.AnesthesiologistId, Is.EqualTo("ANES-001"));
        Assert.That(state.AnesthesiologistName, Is.EqualTo("Dr. Patel"));
    }

    [Test]
    public async Task SurgeryGrain_EndOperation_RecordsEndTime()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-003", "Hip Replacement", null,
            DateTime.UtcNow, null, null, null, "Orthopedics",
            "Hip OA", null, null, null);
        await grain.BeginOperationAsync(DateTime.UtcNow.AddHours(-2), null, null);

        DateTime ended = DateTime.UtcNow;
        await grain.EndOperationAsync(ended);

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.TimeOperationEnded, Is.EqualTo(ended));
    }

    [Test]
    public async Task SurgeryGrain_AddAssistant_SetsFirstAssistant()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-004", "CABG", null,
            DateTime.UtcNow, "SURG-002", "Dr. Torres",
            null, "Cardiothoracic", null, null, null, null);

        await grain.AddAssistantAsync("SURG-003", "Dr. Kim");

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.FirstAssistantId, Is.EqualTo("SURG-003"));
        Assert.That(state.FirstAssistantName, Is.EqualTo("Dr. Kim"));
    }

    [Test]
    public async Task SurgeryGrain_AddOtherProcedure_AppendsToList()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-005", "CABG x3", null,
            DateTime.UtcNow, null, null, null, "Cardiothoracic",
            null, null, null, null);

        await grain.AddOtherProcedureAsync("Vein harvest — saphenous");
        await grain.AddOtherProcedureAsync("Intra-aortic balloon pump placement");

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.OtherProcedures, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SurgeryGrain_RecordOperativeReport_SetsPostOpDiagnosis()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-006", "Colectomy", null,
            DateTime.UtcNow, null, null, null, "General Surgery",
            "Colon cancer", null, null, null);
        await grain.BeginOperationAsync(DateTime.UtcNow.AddHours(-3), null, null);
        await grain.EndOperationAsync(DateTime.UtcNow);

        await grain.RecordOperativeReportAsync(
            "Procedure performed without complication...",
            "Adenocarcinoma, sigmoid colon",
            "CLEAN_CONTAMINATED");

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.OperativeReport, Does.Contain("without complication"));
        Assert.That(state.PostOpDiagnosis, Is.EqualTo("Adenocarcinoma, sigmoid colon"));
        Assert.That(state.WoundClassification, Is.EqualTo("CLEAN_CONTAMINATED"));
    }

    [Test]
    public async Task SurgeryGrain_CompleteAsync_SetsCompletedStatus()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-007", "Knee Arthroscopy", null,
            DateTime.UtcNow, null, null, null, "Orthopedics",
            "Meniscal tear", null, null, null);
        await grain.BeginOperationAsync(DateTime.UtcNow.AddHours(-1), null, null);
        await grain.EndOperationAsync(DateTime.UtcNow);
        await grain.CompleteAsync();

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
    }

    [Test]
    public async Task SurgeryGrain_CancelAsync_SetsCancelledStatus()
    {
        ISurgeryGrain grain = NewGrain();
        await grain.ScheduleSurgeryAsync(
            "PATIENT-008", "Elective Hernia Repair", null,
            DateTime.UtcNow.AddDays(7), null, null, null, "General Surgery",
            "Inguinal hernia", null, null, null);

        await grain.CancelAsync("Patient non-compliant with pre-op instructions");

        SurgeryState state = await grain.GetSurgeryAsync();
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Does.Contain("non-compliant"));
    }
}

[TestFixture]
public class RadiologyGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IRadiologyGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IRadiologyGrain>($"RAD-{Guid.NewGuid()}");

    [Test]
    public async Task RadiologyGrain_OrderStudy_PersistsAllFields()
    {
        IRadiologyGrain grain = NewGrain();

        await grain.OrderStudyAsync(
            "PATIENT-001", "Chest X-Ray PA and Lateral", "RAD-PROC-001",
            "71046", "GENERAL RADIOLOGY",
            "PROV-001", "Dr. Adams",
            "ROUTINE", "Cough x 3 weeks, r/o pneumonia",
            "Persistent cough", "ORDER-001",
            "LOC-001", "Radiology Department");

        RadiologyState state = await grain.GetRadiologyAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.ProcedureName, Is.EqualTo("Chest X-Ray PA and Lateral"));
        Assert.That(state.CptCode, Is.EqualTo("71046"));
        Assert.That(state.ImagingType, Is.EqualTo("GENERAL RADIOLOGY"));
        Assert.That(state.RequestingProviderName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(state.Status, Is.EqualTo("PENDING"));
    }

    [Test]
    public async Task RadiologyGrain_RecordExam_SetsExaminedStatus()
    {
        IRadiologyGrain grain = NewGrain();
        await grain.OrderStudyAsync(
            "PATIENT-002", "CT Head Without Contrast", null, "70450",
            "CT", null, null, "STAT", "Head trauma",
            null, null, null, null);

        DateTime examTime = DateTime.UtcNow;
        await grain.RecordExamAsync(examTime);

        RadiologyState state = await grain.GetRadiologyAsync();
        Assert.That(state.Status, Is.EqualTo("EXAMINED"));
        Assert.That(state.ExamDateTime, Is.EqualTo(examTime));
    }

    [Test]
    public async Task RadiologyGrain_RecordReport_PersistsReportAndImpression()
    {
        IRadiologyGrain grain = NewGrain();
        await grain.OrderStudyAsync(
            "PATIENT-003", "MRI Lumbar Spine", null, "72148",
            "MRI", "PROV-002", "Dr. Baker",
            "ROUTINE", "Low back pain", null, null, null, null);
        await grain.RecordExamAsync(DateTime.UtcNow.AddHours(-1));

        DateTime reportTime = DateTime.UtcNow;
        await grain.RecordReportAsync(
            "L4-L5 disc herniation with moderate foraminal stenosis...",
            "Disc herniation at L4-L5 with nerve impingement.",
            "722.10",
            "RAD-001", "Dr. Nguyen",
            reportTime);

        RadiologyState state = await grain.GetRadiologyAsync();
        Assert.That(state.ReportText, Does.Contain("L4-L5 disc herniation"));
        Assert.That(state.Impression, Does.Contain("Disc herniation"));
        Assert.That(state.DiagnosticCode, Is.EqualTo("722.10"));
        Assert.That(state.InterpretingPhysicianName, Is.EqualTo("Dr. Nguyen"));
        Assert.That(state.ReportDateTime, Is.EqualTo(reportTime));
    }

    [Test]
    public async Task RadiologyGrain_CompleteAsync_SetsCompletedStatus()
    {
        IRadiologyGrain grain = NewGrain();
        await grain.OrderStudyAsync(
            "PATIENT-004", "Bone Scan", null, "78300",
            "NUCLEAR MEDICINE", null, null, "ROUTINE",
            null, null, null, null, null);
        await grain.RecordExamAsync(DateTime.UtcNow.AddHours(-2));
        await grain.RecordReportAsync(
            "No evidence of metastatic disease.", "Negative bone scan.",
            null, null, null, DateTime.UtcNow);
        await grain.CompleteAsync();

        RadiologyState state = await grain.GetRadiologyAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
    }

    [Test]
    public async Task RadiologyGrain_CancelAsync_SetsCancelledStatus()
    {
        IRadiologyGrain grain = NewGrain();
        await grain.OrderStudyAsync(
            "PATIENT-005", "PET Scan", null, "78816",
            "NUCLEAR MEDICINE", null, null, "ROUTINE",
            null, null, null, null, null);

        await grain.CancelAsync();

        RadiologyState state = await grain.GetRadiologyAsync();
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
    }

    [Test]
    public async Task RadiologyGrain_FullLifecycle_OrderedToComplete()
    {
        IRadiologyGrain grain = NewGrain();

        await grain.OrderStudyAsync(
            "PATIENT-006", "Echocardiogram", null, "93306",
            "ULTRASOUND", "PROV-003", "Dr. Carter",
            "ROUTINE", "Evaluate cardiac function", "SOB",
            "ORDER-006", "LOC-002", "Echo Lab");

        RadiologyState state = await grain.GetRadiologyAsync();
        Assert.That(state.Status, Is.EqualTo("PENDING"));

        await grain.RecordExamAsync(DateTime.UtcNow.AddHours(-3));
        state = await grain.GetRadiologyAsync();
        Assert.That(state.Status, Is.EqualTo("EXAMINED"));

        await grain.RecordReportAsync(
            "EF 55%. Normal wall motion. No valvular abnormality.",
            "Normal echocardiogram.",
            null, "RAD-002", "Dr. Walsh", DateTime.UtcNow);
        await grain.CompleteAsync();

        state = await grain.GetRadiologyAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.ReportText, Does.Contain("EF 55%"));
    }
}

[TestFixture]
public class ImagingGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IImagingGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IImagingGrain>($"IMG-{Guid.NewGuid()}");

    [Test]
    public async Task ImagingGrain_CaptureImage_PersistsAllFields()
    {
        IImagingGrain grain = NewGrain();
        DateTime captureDate = DateTime.UtcNow;
        DateTime procDate = DateTime.UtcNow.AddHours(-1);

        await grain.CaptureImageAsync(
            "PATIENT-001", "DICOM", "CT Chest",
            "RADIOLOGY", "https://storage.example.com/images/ct-chest-001.dcm",
            "https://storage.example.com/thumbs/ct-chest-001.jpg",
            "1.2.3.4.5.6", "1.2.3.4.5",
            procDate, captureDate, 120,
            "RAD-001", null,
            "TECH-001", "Johnson, Mary",
            "LOC-001", "Radiology", "Routine CT");

        ImagingState state = await grain.GetImageAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.ObjectType, Is.EqualTo("DICOM"));
        Assert.That(state.ProcedureDescription, Is.EqualTo("CT Chest"));
        Assert.That(state.DicomSeriesUid, Is.EqualTo("1.2.3.4.5.6"));
        Assert.That(state.DicomStudyUid, Is.EqualTo("1.2.3.4.5"));
        Assert.That(state.ImageCount, Is.EqualTo(120));
        Assert.That(state.CapturedByName, Is.EqualTo("Johnson, Mary"));
        Assert.That(state.RadiologyId, Is.EqualTo("RAD-001"));
        Assert.That(state.Status, Is.EqualTo("VIEWABLE"));
    }

    [Test]
    public async Task ImagingGrain_MarkForReview_SetsReviewStatus()
    {
        IImagingGrain grain = NewGrain();
        await grain.CaptureImageAsync(
            "PATIENT-002", "DICOM", null, null,
            null, null, null, null,
            null, DateTime.UtcNow, 1,
            null, null, null, null, null, null, null);

        await grain.MarkForReviewAsync();

        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.Status, Is.EqualTo("NEEDS REVIEW"));
    }

    [Test]
    public async Task ImagingGrain_QaReview_SetsReviewedStatus()
    {
        IImagingGrain grain = NewGrain();
        await grain.CaptureImageAsync(
            "PATIENT-003", "PHOTO", "Wound", null,
            "https://storage.example.com/images/wound-001.jpg",
            "https://storage.example.com/thumbs/wound-001.jpg",
            null, null, null, DateTime.UtcNow, 1,
            null, null, "NURSE-001", "Smith, Jane",
            "LOC-002", "Wound Clinic", null);

        await grain.MarkForReviewAsync();
        await grain.QaReviewAsync();

        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.Status, Is.EqualTo("QA REVIEWED"));
    }

    [Test]
    public async Task ImagingGrain_DeleteImage_SetsDeletedStatus()
    {
        IImagingGrain grain = NewGrain();
        await grain.CaptureImageAsync(
            "PATIENT-004", "DICOM", null, null,
            null, null, null, null,
            null, DateTime.UtcNow, 1,
            null, null, null, null, null, null, null);

        await grain.DeleteImageAsync();

        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.Status, Is.EqualTo("DELETED"));
    }

    [Test]
    public async Task ImagingGrain_LinkedToTiuDocument_PersistsLink()
    {
        IImagingGrain grain = NewGrain();
        await grain.CaptureImageAsync(
            "PATIENT-005", "DICOM", "Pathology Slide", "PATHOLOGY",
            null, null, null, null,
            DateTime.UtcNow, DateTime.UtcNow, 24,
            null, "TIU-001",
            "PATH-001", "Williams, Tom",
            "LOC-003", "Pathology Lab", "Biopsy specimen");

        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.TiuDocumentId, Is.EqualTo("TIU-001"));
        Assert.That(state.SpecialtyIndex, Is.EqualTo("PATHOLOGY"));
    }
}
