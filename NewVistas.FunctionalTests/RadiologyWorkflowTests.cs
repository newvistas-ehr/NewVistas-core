// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Radiology — File #75.1.
/// Tests Radiology grain methods directly for ordering, exam, reporting,
/// contrast, radiation dose, critical results, and full lifecycle workflows.
/// </summary>
[TestFixture]
public class RadiologyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IRadiologyGrain NewRadiologyGrain()
        => _cluster.GrainFactory.GetGrain<IRadiologyGrain>($"RAD-{Guid.NewGuid():N}");

    private async Task OrderStandardStudyAsync(IRadiologyGrain grain,
        string procedureName = "CHEST 2 VIEWS PA AND LATERAL",
        string imagingType = "GENERAL RADIOLOGY",
        string urgency = "ROUTINE")
    {
        await grain.OrderStudyAsync(
            "PAT-001", procedureName, $"PROC-{Guid.NewGuid():N}",
            "71046", imagingType,
            "PROV-001", "Dr. Adams",
            urgency,
            "Patient with persistent cough for 3 weeks",
            "Rule out pneumonia",
            null, "LOC-RAD-01", "Radiology Department");
    }

    // ─── 1. Order Study ─────────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanOrderStudy()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();

        // Act
        await OrderStandardStudyAsync(grain);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.ProcedureName, Is.EqualTo("CHEST 2 VIEWS PA AND LATERAL"));
        Assert.That(state.ImagingType, Is.EqualTo("GENERAL RADIOLOGY"));
        Assert.That(state.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(state.RequestingProviderName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.OrderDateTime, Is.Not.Null);
    }

    // ─── 2. Record Exam ─────────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordExam()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        DateTime examDateTime = DateTime.UtcNow;

        // Act
        await grain.RecordExamAsync(examDateTime);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("EXAMINED"));
        Assert.That(state.ExamDateTime, Is.Not.Null);
        Assert.That(state.ExamDateTime!.Value, Is.EqualTo(examDateTime).Within(TimeSpan.FromSeconds(1)));
    }

    // ─── 3. Record Report ───────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordReport()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);
        string reportText = "Clear lung fields bilaterally. No consolidation or effusion.";
        string impression = "Normal chest radiograph.";

        // Act
        await grain.RecordReportAsync(reportText, impression, null, "RAD-001", "Dr. Kim", DateTime.UtcNow);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.ReportText, Is.EqualTo(reportText));
        Assert.That(state.Impression, Is.EqualTo(impression));
        Assert.That(state.InterpretingPhysicianName, Is.EqualTo("Dr. Kim"));
        Assert.That(state.ReportDateTime, Is.Not.Null);
    }

    // ─── 4. Complete ────────────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanComplete()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);
        await grain.RecordReportAsync("Normal findings.", "No acute disease.", null, "RAD-001", "Dr. Kim", DateTime.UtcNow);

        // Act
        await grain.CompleteAsync();
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
    }

    // ─── 5. Cancel ──────────────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanCancel()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);

        // Act
        await grain.CancelAsync();
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
    }

    // ─── 6. Record Contrast ────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordContrast()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain, procedureName: "CT ABDOMEN WITH CONTRAST", imagingType: "CT SCAN");

        // Act
        await grain.RecordContrastAsync("Omnipaque 300", "IV", 100.0);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.ContrastAgent, Is.EqualTo("Omnipaque 300"));
        Assert.That(state.ContrastRoute, Is.EqualTo("IV"));
        Assert.That(state.ContrastVolumeMl, Is.EqualTo(100.0));
    }

    // ─── 7. Record Contrast Reaction ────────────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordContrastReaction()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain, imagingType: "CT SCAN");
        await grain.RecordContrastAsync("Omnipaque 300", "IV", 100.0);

        // Act
        await grain.RecordContrastReactionAsync("Mild urticaria, treated with diphenhydramine 25mg IV");
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.ContrastReactionOccurred, Is.True);
        Assert.That(state.ContrastReactionDetails, Is.EqualTo("Mild urticaria, treated with diphenhydramine 25mg IV"));
    }

    // ─── 8. Record Radiation Dose ───────────────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordRadiationDose()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain, procedureName: "CT HEAD WITHOUT CONTRAST", imagingType: "CT SCAN");

        // Act
        await grain.RecordRadiationDoseAsync(2.0, 45.5, 850.0);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.RadiationDoseMSv, Is.EqualTo(2.0));
        Assert.That(state.CtdiVol, Is.EqualTo(45.5));
        Assert.That(state.DoseLengthProduct, Is.EqualTo(850.0));
    }

    // ─── 9. Sign Report ────────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanSignReport()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);
        await grain.RecordReportAsync("Normal chest.", "No acute disease.", null, "RAD-001", "Dr. Kim", DateTime.UtcNow);
        DateTime signedDateTime = DateTime.UtcNow;

        // Act
        await grain.SignReportAsync("RAD-001", "Dr. Kim", signedDateTime);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.ReportStatus, Is.EqualTo("FINAL"));
        Assert.That(state.SignedById, Is.EqualTo("RAD-001"));
        Assert.That(state.SignedByName, Is.EqualTo("Dr. Kim"));
        Assert.That(state.SignedDateTime, Is.Not.Null);
    }

    // ─── 10. Flag Critical Result ───────────────────────────────────────────

    [Test]
    public async Task Radiology_CanFlagCriticalResult()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);

        // Act
        await grain.FlagCriticalResultAsync();
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.IsCriticalResult, Is.True);
    }

    // ─── 11. Record Critical Notification ───────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordCriticalNotification()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);
        await grain.FlagCriticalResultAsync();
        DateTime notifiedDateTime = DateTime.UtcNow;

        // Act
        await grain.RecordCriticalResultNotificationAsync("Dr. Adams", notifiedDateTime);
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.CriticalResultNotifiedTo, Is.EqualTo("Dr. Adams"));
        Assert.That(state.CriticalResultNotifiedDateTime, Is.Not.Null);
        Assert.That(state.CriticalResultNotifiedDateTime!.Value, Is.EqualTo(notifiedDateTime).Within(TimeSpan.FromSeconds(1)));
    }

    // ─── 12. Acknowledge Critical ──────────────────────────────────────────

    [Test]
    public async Task Radiology_CanAcknowledgeCritical()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);
        await grain.FlagCriticalResultAsync();
        await grain.RecordCriticalResultNotificationAsync("Dr. Adams", DateTime.UtcNow);

        // Act
        await grain.AcknowledgeCriticalResultAsync("Dr. Adams");
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.CriticalResultAcknowledgedBy, Is.EqualTo("Dr. Adams"));
    }

    // ─── 13. Record Technician ──────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanRecordTechnician()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);

        // Act
        await grain.RecordTechnicianAsync("TECH-001", "Jane Wilson RT");
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.TechnicianId, Is.EqualTo("TECH-001"));
        Assert.That(state.TechnicianName, Is.EqualTo("Jane Wilson RT"));
    }

    // ─── 14. Set Body Part and Laterality ───────────────────────────────────

    [Test]
    public async Task Radiology_CanSetBodyPartAndLaterality()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain, procedureName: "MRI KNEE", imagingType: "MRI");

        // Act
        await grain.SetBodyPartAndLateralityAsync("KNEE", "RIGHT");
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.BodyPart, Is.EqualTo("KNEE"));
        Assert.That(state.Laterality, Is.EqualTo("RIGHT"));
    }

    // ─── 15. Amend Report ──────────────────────────────────────────────────

    [Test]
    public async Task Radiology_CanAmendReport()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();
        await OrderStandardStudyAsync(grain);
        await grain.RecordExamAsync(DateTime.UtcNow);
        await grain.RecordReportAsync("Normal chest.", "No acute disease.", null, "RAD-001", "Dr. Kim", DateTime.UtcNow);
        await grain.SignReportAsync("RAD-001", "Dr. Kim", DateTime.UtcNow);

        // Act
        await grain.AmendReportAsync("Addendum: Small left pleural effusion noted on review.");
        RadiologyState state = await grain.GetRadiologyAsync();

        // Assert
        Assert.That(state.ReportStatus, Is.EqualTo("AMENDED"));
        Assert.That(state.AmendmentText, Is.EqualTo("Addendum: Small left pleural effusion noted on review."));
        Assert.That(state.AmendmentDateTime, Is.Not.Null);
    }

    // ─── 16. Full Workflow ──────────────────────────────────────────────────

    [Test]
    public async Task Radiology_FullWorkflow()
    {
        // Arrange
        IRadiologyGrain grain = NewRadiologyGrain();

        // Order
        await grain.OrderStudyAsync(
            "PAT-FULL", "CT ABDOMEN PELVIS WITH CONTRAST", "PROC-CT-01",
            "74177", "CT SCAN",
            "PROV-010", "Dr. Chen",
            "STAT",
            "Acute abdominal pain with elevated WBC",
            "Rule out appendicitis",
            null, "LOC-RAD-02", "CT Suite");
        RadiologyState s1 = await grain.GetRadiologyAsync();
        Assert.That(s1.Status, Is.EqualTo("PENDING"));

        // Record exam
        await grain.RecordExamAsync(DateTime.UtcNow);
        RadiologyState s2 = await grain.GetRadiologyAsync();
        Assert.That(s2.Status, Is.EqualTo("EXAMINED"));

        // Technician and body part
        await grain.RecordTechnicianAsync("TECH-010", "Bob Martinez RT");
        await grain.SetBodyPartAndLateralityAsync("ABDOMEN", "N/A");

        // Contrast
        await grain.RecordContrastAsync("Omnipaque 350", "IV", 120.0);

        // Radiation dose
        await grain.RecordRadiationDoseAsync(8.0, 12.5, 600.0);

        // Report
        await grain.RecordReportAsync(
            "CT abdomen and pelvis with IV contrast. Enlarged appendix measuring 12mm with periappendiceal fat stranding.",
            "Acute appendicitis. No perforation.",
            "K35.80", "RAD-010", "Dr. Garcia", DateTime.UtcNow);

        // Sign report
        await grain.SignReportAsync("RAD-010", "Dr. Garcia", DateTime.UtcNow);

        // Flag and notify critical result
        await grain.FlagCriticalResultAsync();
        await grain.RecordCriticalResultNotificationAsync("Dr. Chen", DateTime.UtcNow);
        await grain.AcknowledgeCriticalResultAsync("Dr. Chen");

        // Complete
        await grain.CompleteAsync();

        // Final assertions
        RadiologyState finalState = await grain.GetRadiologyAsync();
        Assert.That(finalState.Status, Is.EqualTo("COMPLETE"));
        Assert.That(finalState.ReportStatus, Is.EqualTo("FINAL"));
        Assert.That(finalState.ReportText, Does.Contain("appendix"));
        Assert.That(finalState.Impression, Does.Contain("appendicitis"));
        Assert.That(finalState.ContrastAgent, Is.EqualTo("Omnipaque 350"));
        Assert.That(finalState.RadiationDoseMSv, Is.EqualTo(8.0));
        Assert.That(finalState.IsCriticalResult, Is.True);
        Assert.That(finalState.CriticalResultAcknowledgedBy, Is.EqualTo("Dr. Chen"));
        Assert.That(finalState.TechnicianName, Is.EqualTo("Bob Martinez RT"));
        Assert.That(finalState.BodyPart, Is.EqualTo("ABDOMEN"));
        Assert.That(finalState.SignedByName, Is.EqualTo("Dr. Garcia"));
    }
}
