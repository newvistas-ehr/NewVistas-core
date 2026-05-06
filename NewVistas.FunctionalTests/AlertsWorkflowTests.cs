// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Alerts / Notifications — VistA ORB NOTIFICATION file (#8992.1).
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class AlertsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Create Alert ────────────────────────────────────────────────────────

    [Test]
    public async Task CreateAlert_ReturnsNonEmptyId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.LabResults,
            "Lab Results",
            "USR-100", "Dr. Smith",
            "LAB", "New lab results available",
            "Review results", false, null);

        Assert.That(alertId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task CreateAlert_StoresNotificationTypeText()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.AbnormalLabResults,
            "Abnormal Lab Results",
            "USR-200", "Dr. Jones",
            "LAB", "Abnormal potassium level",
            null, false, null);

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.NotificationTypeText, Is.EqualTo("Abnormal Lab Results"));
    }

    [Test]
    public async Task CreateAlert_StoresRecipientInfo()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.ImagingResults,
            "Imaging Results",
            "USR-300", "Dr. Williams",
            "RADIOLOGY", "CT scan complete",
            null, false, null);

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.RecipientId, Is.EqualTo("USR-300"));
        Assert.That(state.RecipientName, Is.EqualTo("Dr. Williams"));
    }

    [Test]
    public async Task CreateAlert_StoresMessageText()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.NewOrder,
            "New Order",
            "USR-400", "Dr. Brown",
            "OR", "New medication order entered",
            null, false, null);

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.MessageText, Is.EqualTo("New medication order entered"));
    }

    [Test]
    public async Task CreateAlert_CriticalFlagStored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.CriticalLabResults,
            "Critical Lab Results",
            "USR-500", "Dr. Davis",
            "LAB", "Critical troponin level",
            "Immediate review required", true, "LAB-IEN-999");

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.IsCritical, Is.True);
    }

    [Test]
    public async Task GetAlert_RetrievesFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.ConsultRequestResolution,
            "Consult Request Resolution",
            "USR-600", "Dr. Wilson",
            "GMRC", "Cardiology consult completed",
            "Open consult detail", false, "CONSULT-IEN-42");

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.AlertId, Is.EqualTo(alertId));
        Assert.That(state.NotificationType, Is.EqualTo(NotificationType.ConsultRequestResolution));
        Assert.That(state.NotificationTypeText, Is.EqualTo("Consult Request Resolution"));
        Assert.That(state.RecipientId, Is.EqualTo("USR-600"));
        Assert.That(state.RecipientName, Is.EqualTo("Dr. Wilson"));
        Assert.That(state.SendingPackage, Is.EqualTo("GMRC"));
        Assert.That(state.MessageText, Is.EqualTo("Cardiology consult completed"));
        Assert.That(state.FollowUpAction, Is.EqualTo("Open consult detail"));
        Assert.That(state.IsCritical, Is.False);
        Assert.That(state.XqaData, Is.EqualTo("CONSULT-IEN-42"));
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // ── Process Alert ───────────────────────────────────────────────────────

    [Test]
    public async Task ProcessAlert_SetsProcessedFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.StatResults,
            "Stat Results",
            "USR-700", "Dr. Taylor",
            "LAB", "Stat CBC resulted",
            null, false, null);

        DateTime processedAt = DateTime.UtcNow;
        await wf.ProcessAlertAsync(alertId, processedAt, "USR-700");

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.ProcessedDateTime, Is.EqualTo(processedAt));
        Assert.That(state.ProcessedByUserId, Is.EqualTo("USR-700"));
        Assert.That(state.Status, Is.EqualTo("PROCESSED"));
    }

    // ── Delete Alert ────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAlert_SetsDeletedFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.FlaggedOrders,
            "Flagged Orders",
            "USR-800", "Dr. Anderson",
            "OR", "Order flagged for review",
            null, false, null);

        await wf.DeleteAlertAsync(alertId, "USR-800");

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.DeletedByUserId, Is.EqualTo("USR-800"));
        Assert.That(state.DeletedDateTime, Is.Not.Null);
        Assert.That(state.Status, Is.EqualTo("DELETED"));
    }

    // ── Forward Alert ───────────────────────────────────────────────────────

    [Test]
    public async Task ForwardAlert_SetsForwardFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.NewServiceConsultRequest,
            "New Service Consult Request",
            "USR-900", "Dr. Thomas",
            "GMRC", "New cardiology consult",
            null, false, null);

        await wf.ForwardAlertAsync(alertId, "USR-901", "Dr. Martinez",
            AlertForwardType.ForwardOnly, null, "USR-900");

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.ForwardHistory, Has.Count.EqualTo(1));
        Assert.That(state.ForwardHistory[0].ToRecipientId, Is.EqualTo("USR-901"));
        Assert.That(state.ForwardHistory[0].ToRecipientName, Is.EqualTo("Dr. Martinez"));
        Assert.That(state.ForwardHistory[0].ForwardType, Is.EqualTo(AlertForwardType.ForwardOnly));
        Assert.That(state.ForwardHistory[0].ForwardedByUserId, Is.EqualTo("USR-900"));
    }

    [Test]
    public async Task ForwardAlert_WithComment()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string alertId = await wf.CreateAlertAsync(
            NotificationType.OrderRequiresElecSignature,
            "Order Requires Electronic Signature",
            "USR-1000", "Dr. Garcia",
            "OR", "Order awaiting signature",
            null, false, null);

        await wf.ForwardAlertAsync(alertId, "USR-1001", "Dr. Robinson",
            AlertForwardType.SendAndKeep, "Please review and cosign this order", "USR-1000");

        NotificationState state = await wf.GetAlertAsync(alertId);

        Assert.That(state.ForwardHistory, Has.Count.EqualTo(1));
        Assert.That(state.ForwardHistory[0].Comment, Is.EqualTo("Please review and cosign this order"));
        Assert.That(state.ForwardHistory[0].ForwardType, Is.EqualTo(AlertForwardType.SendAndKeep));
    }

    // ── Full Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_CreateProcessDelete()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Create
        string alertId = await wf.CreateAlertAsync(
            NotificationType.DnrExpiring,
            "DNR Expiring",
            "USR-1100", "Dr. Lee",
            "OR", "DNR order expiring in 24 hours",
            "Review DNR order", true, "ORDER-IEN-555");

        NotificationState created = await wf.GetAlertAsync(alertId);
        Assert.That(created.Status, Is.EqualTo("ACTIVE"));
        Assert.That(created.IsCritical, Is.True);

        // Process
        DateTime processedAt = DateTime.UtcNow;
        await wf.ProcessAlertAsync(alertId, processedAt, "USR-1100");

        NotificationState processed = await wf.GetAlertAsync(alertId);
        Assert.That(processed.Status, Is.EqualTo("PROCESSED"));
        Assert.That(processed.ProcessedByUserId, Is.EqualTo("USR-1100"));
        Assert.That(processed.ProcessedDateTime, Is.EqualTo(processedAt));

        // Delete
        await wf.DeleteAlertAsync(alertId, "USR-1100");

        NotificationState deleted = await wf.GetAlertAsync(alertId);
        Assert.That(deleted.Status, Is.EqualTo("DELETED"));
        Assert.That(deleted.DeletedByUserId, Is.EqualTo("USR-1100"));
        Assert.That(deleted.DeletedDateTime, Is.Not.Null);
    }
}
