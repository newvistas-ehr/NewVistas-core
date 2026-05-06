// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Women's Health — VistA File #790.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class WomensHealthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Mammography notifications ─────────────────────────────────────────────

    [Test]
    public async Task CreateMammographyNotification_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            new DateTime(2024, 3, 15),
            "PROV-001", "Dr. Radiology",
            "LOC-001", "Breast Imaging Center",
            MammographyResult.Normal,
            1,
            papSmearResult: null,
            contraceptiveMethod: null,
            gestationalAgeWeeks: null,
            estimatedDueDate: null,
            pregnancyOutcome: null,
            followUpRequired: false,
            nextDueDate: new DateTime(2025, 3, 15),
            isRefusal: false,
            notes: "Annual screening mammogram — normal bilateral");

        Assert.That(notificationId, Does.StartWith("WH-NOTE:"));

        List<WomensHealthIndexEntry> all = await wf.GetWomensHealthNotificationsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].NotificationId, Is.EqualTo(notificationId));
        Assert.That(all[0].NotificationType, Is.EqualTo(WomensHealthNotificationType.Mammography));
        Assert.That(all[0].Status, Is.EqualTo(WomensHealthNotificationStatus.Active));
    }

    [Test]
    public async Task CreateMammography_WithFollowUpRequired_SetsStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            "PROV-002", "Dr. Breast Imager",
            null, null,
            MammographyResult.SuspiciousAbnormality,
            4,
            null, null, null, null, null,
            followUpRequired: true,
            nextDueDate: DateTime.UtcNow.AddDays(14),
            isRefusal: false,
            notes: "BI-RADS 4 — biopsy recommended");

        List<WomensHealthIndexEntry> all = await wf.GetWomensHealthNotificationsAsync();
        Assert.That(all[0].Status, Is.EqualTo(WomensHealthNotificationStatus.FollowUpRequired));
        Assert.That(all[0].FollowUpRequired, Is.True);
        Assert.That(all[0].NextDueDate, Is.Not.Null);
    }

    // ── Pap smear notifications ───────────────────────────────────────────────

    [Test]
    public async Task CreatePapSmearNotification_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.PapSmear,
            new DateTime(2024, 6, 1),
            "PROV-003", "Dr. GYN",
            "LOC-002", "Women's Health Clinic",
            mammographyResult: null,
            biRadsScore: null,
            papSmearResult: PapSmearResult.Normal,
            contraceptiveMethod: null,
            gestationalAgeWeeks: null,
            estimatedDueDate: null,
            pregnancyOutcome: null,
            followUpRequired: false,
            nextDueDate: new DateTime(2027, 6, 1),
            isRefusal: false,
            notes: "Routine Pap — normal cytology");

        WomensHealthNotificationState state = await wf.GetWomensHealthNotificationAsync(notificationId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.NotificationType, Is.EqualTo(WomensHealthNotificationType.PapSmear));
        Assert.That(state.PapSmearResult, Is.EqualTo(PapSmearResult.Normal));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. GYN"));
        Assert.That(state.LocationName, Is.EqualTo("Women's Health Clinic"));
    }

    [Test]
    public async Task CreatePapSmear_AbnormalResult_WithFollowUp()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.PapSmear,
            DateTime.UtcNow,
            null, null, null, null,
            null, null,
            PapSmearResult.Lsil,
            null, null, null, null,
            followUpRequired: true,
            nextDueDate: DateTime.UtcNow.AddMonths(6),
            isRefusal: false,
            notes: "LSIL — repeat in 6 months");

        List<WomensHealthIndexEntry> followUps = await wf.GetWomensHealthFollowUpRequiredAsync();
        Assert.That(followUps, Has.Count.EqualTo(1));
        Assert.That(followUps[0].FollowUpRequired, Is.True);
    }

    // ── Completion ────────────────────────────────────────────────────────────

    [Test]
    public async Task CompleteNotification_SetsStatusCompleted()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            MammographyResult.ProbablyBenign, 3,
            null, null, null, null, null,
            followUpRequired: true,
            nextDueDate: DateTime.UtcNow.AddMonths(6),
            isRefusal: false,
            notes: "BI-RADS 3 — short interval follow-up");

        await wf.CompleteWomensHealthNotificationAsync(
            notificationId,
            DateTime.UtcNow,
            "Follow-up mammogram performed — now BI-RADS 2");

        WomensHealthNotificationState state = await wf.GetWomensHealthNotificationAsync(notificationId);
        Assert.That(state.Status, Is.EqualTo(WomensHealthNotificationStatus.Completed));
        Assert.That(state.FollowUpCompletedDate, Is.Not.Null);

        List<WomensHealthIndexEntry> index = await wf.GetWomensHealthNotificationsAsync();
        Assert.That(index[0].Status, Is.EqualTo(WomensHealthNotificationStatus.Completed));
        Assert.That(index[0].FollowUpRequired, Is.False);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Test]
    public async Task CancelNotification_SetsStatusCancelled()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Contraception,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null,
            "IUD",
            null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: false,
            notes: "IUD counseling visit");

        await wf.CancelWomensHealthNotificationAsync(notificationId);

        WomensHealthNotificationState state = await wf.GetWomensHealthNotificationAsync(notificationId);
        Assert.That(state.Status, Is.EqualTo(WomensHealthNotificationStatus.Cancelled));

        List<WomensHealthIndexEntry> index = await wf.GetWomensHealthNotificationsAsync();
        Assert.That(index[0].Status, Is.EqualTo(WomensHealthNotificationStatus.Cancelled));
    }

    // ── Follow-up management ──────────────────────────────────────────────────

    [Test]
    public async Task SetFollowUp_UpdatesFollowUpFlagAndDate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.BreastHealth,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null, null, null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: false,
            notes: "Breast exam — benign findings");

        DateTime followUpDate = DateTime.UtcNow.AddMonths(12);
        await wf.SetWomensHealthFollowUpAsync(notificationId, true, followUpDate);

        WomensHealthNotificationState state = await wf.GetWomensHealthNotificationAsync(notificationId);
        Assert.That(state.FollowUpRequired, Is.True);
        Assert.That(state.NextDueDate, Is.Not.Null);

        List<WomensHealthIndexEntry> followUps = await wf.GetWomensHealthFollowUpRequiredAsync();
        Assert.That(followUps, Has.Count.EqualTo(1));

        // Clear follow-up
        await wf.SetWomensHealthFollowUpAsync(notificationId, false, null);
        List<WomensHealthIndexEntry> noFollowUps = await wf.GetWomensHealthFollowUpRequiredAsync();
        Assert.That(noFollowUps, Has.Count.EqualTo(0));
    }

    // ── Filter by type ────────────────────────────────────────────────────────

    [Test]
    public async Task GetByType_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            MammographyResult.Normal, 1,
            null, null, null, null, null,
            false, null, false, null);

        await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.PapSmear,
            DateTime.UtcNow,
            null, null, null, null,
            null, null,
            PapSmearResult.Normal,
            null, null, null, null,
            false, null, false, null);

        await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            MammographyResult.BenignFinding, 2,
            null, null, null, null, null,
            false, null, false, null);

        List<WomensHealthIndexEntry> mammo = await wf.GetWomensHealthNotificationsByTypeAsync(
            WomensHealthNotificationType.Mammography);
        List<WomensHealthIndexEntry> pap = await wf.GetWomensHealthNotificationsByTypeAsync(
            WomensHealthNotificationType.PapSmear);

        Assert.That(mammo, Has.Count.EqualTo(2));
        Assert.That(pap, Has.Count.EqualTo(1));
    }

    // ── Pregnancy notification ────────────────────────────────────────────────

    [Test]
    public async Task CreatePregnancyNotification_StoresObFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Pregnancy,
            DateTime.UtcNow,
            "PROV-010", "Dr. OB",
            null, null,
            null, null, null, null,
            gestationalAgeWeeks: 12,
            estimatedDueDate: DateTime.UtcNow.AddDays(196),
            pregnancyOutcome: "ONGOING",
            followUpRequired: true,
            nextDueDate: DateTime.UtcNow.AddDays(28),
            isRefusal: false,
            notes: "First prenatal visit");

        WomensHealthNotificationState state = await wf.GetWomensHealthNotificationAsync(notificationId);
        Assert.That(state.NotificationType, Is.EqualTo(WomensHealthNotificationType.Pregnancy));
        Assert.That(state.GestationalAgeWeeks, Is.EqualTo(12));
        Assert.That(state.EstimatedDueDate, Is.Not.Null);
        Assert.That(state.PregnancyOutcome, Is.EqualTo("ONGOING"));
    }

    // ── Refusal ───────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateRefusal_SetsIsRefusalFlag()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string notificationId = await wf.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null, null, null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: true,
            notes: "Patient declined screening mammogram");

        WomensHealthNotificationState state = await wf.GetWomensHealthNotificationAsync(notificationId);
        Assert.That(state.IsRefusal, Is.True);
        Assert.That(state.Notes, Does.Contain("declined"));
    }

    // ── Independent patients ──────────────────────────────────────────────────

    [Test]
    public async Task DifferentPatients_HaveIndependentNotifications()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            MammographyResult.Normal, 1,
            null, null, null, null, null,
            false, null, false, null);

        await wf2.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.PapSmear,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, PapSmearResult.Ascus,
            null, null, null, null,
            true, DateTime.UtcNow.AddMonths(3), false, null);

        await wf2.CreateWomensHealthNotificationAsync(
            WomensHealthNotificationType.Contraception,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null,
            "Oral Contraceptives",
            null, null, null,
            false, null, false, null);

        List<WomensHealthIndexEntry> p1Notes = await wf1.GetWomensHealthNotificationsAsync();
        List<WomensHealthIndexEntry> p2Notes = await wf2.GetWomensHealthNotificationsAsync();

        Assert.That(p1Notes, Has.Count.EqualTo(1));
        Assert.That(p2Notes, Has.Count.EqualTo(2));
    }
}
