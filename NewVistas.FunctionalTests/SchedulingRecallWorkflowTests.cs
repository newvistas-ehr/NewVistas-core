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
/// Functional tests for the previously uncovered scheduling and recall workflow methods on
/// PatientWorkflowGrain: RescheduleAppointmentAsync, CancelAppointmentWithReasonAsync,
/// ReassignAppointmentProviderAsync (CprsWorkflow partial), and ScheduleRecallAppointmentAsync,
/// RecordRecallContactAttemptAsync, CancelRecallEntryAsync (Recall partial).
///
/// The recall surface is gated by the PATIENT_RECALL site flag (off by default), which lives on
/// the shared "SITE:DEFAULT" grain — so this fixture is NonParallelizable and (re)enables the
/// flag around every test, following the BoneHealthWorkflowTests pattern.
/// </summary>
[TestFixture, NonParallelizable]
public class SchedulingRecallWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string RecallFeature = "PATIENT_RECALL";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(RecallFeature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(RecallFeature);

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IProviderScheduleIndexGrain ProviderSchedule(string providerId) =>
        _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>($"PROV-SCHED:{providerId}");

    private IScheduleIndexGrain ClinicSchedule(string clinicId) =>
        _cluster.GrainFactory.GetGrain<IScheduleIndexGrain>($"CLINIC-SCHED:{clinicId}");

    private IProviderPatientIndexGrain ProviderPatients(string providerId) =>
        _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{providerId}");

    private ICareTeamGrain CareTeam(string patientId) =>
        _cluster.GrainFactory.GetGrain<ICareTeamGrain>($"CARE-TEAM:{patientId}");

    private async Task<string> NewPatientAsync()
    {
        string patientId = $"SRPAT-{Guid.NewGuid()}";
        await Workflow(patientId).UpdateDemographicsAsync(
            "SCHEDTEST,PATIENT", "M", new DateTime(1975, 6, 15), null);
        return patientId;
    }

    // ─── RescheduleAppointmentAsync ──────────────────────────────────────────

    [Test]
    public async Task Reschedule_MovesAppointment_AndSyncsPatientAndProviderIndexes()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        string providerId = $"PROV-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);

        DateTime originalTime = DateTime.UtcNow.Date.AddDays(20).AddHours(9);
        DateTime newTime = DateTime.UtcNow.Date.AddDays(21).AddHours(14);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "RESCHED CLINIC", originalTime, 30, providerId, "WELBY,MARCUS",
            "Follow-up", "REGULAR");

        await workflow.RescheduleAppointmentAsync(apptId, newTime, "Patient request", "CLERK-1");

        // Appointment grain carries the new time and stays Scheduled
        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.AppointmentDateTime, Is.EqualTo(newTime));
        Assert.That(state.Status, Is.EqualTo("Scheduled"));
        Assert.That(state.ModifiedBy, Is.EqualTo("CLERK-1"));

        // The clinical Purpose survives the reschedule — the reschedule reason is audit
        // trail and lands in Notes, never in Purpose.
        Assert.That(state.Purpose, Is.EqualTo("Follow-up"));
        Assert.That(state.Notes, Does.Contain("Patient request"));

        // Clinic schedule index moved to the new time: the vacated slot is free again
        // (no overlap) and the new time is now protected by the overlap check.
        Assert.That(await ClinicSchedule(clinicId).HasOverlapAsync(originalTime, 30), Is.False,
            "the vacated slot must no longer be blocked");
        Assert.That(await ClinicSchedule(clinicId).HasOverlapAsync(newTime, 30), Is.True,
            "the new time must be protected against double booking");
        List<ClinicScheduleEntry> clinicDay = await ClinicSchedule(clinicId).GetByDateAsync(newTime.Date);
        ClinicScheduleEntry clinicEntry = clinicDay.Single(e => e.AppointmentId == apptId);
        Assert.That(clinicEntry.AppointmentDateTime, Is.EqualTo(newTime));

        // Patient schedule index reflects the new time
        List<AppointmentEntry> entries = await workflow.GetAllAppointmentsAsync();
        AppointmentEntry entry = entries.Single(e => e.AppointmentId == apptId);
        Assert.That(entry.AppointmentDateTime, Is.EqualTo(newTime));
        Assert.That(entry.Status, Is.EqualTo("Scheduled"));

        // Provider schedule index reflects the new time
        List<ProviderScheduleEntry> provEntries = await ProviderSchedule(providerId).GetAllAsync();
        ProviderScheduleEntry provEntry = provEntries.Single(e => e.AppointmentId == apptId);
        Assert.That(provEntry.AppointmentDateTime, Is.EqualTo(newTime));

        // Provider-panel side effects: care team membership and next-appointment date
        Assert.That(await CareTeam(patientId).HasActiveMemberAsync(providerId), Is.True);
        List<ProviderPatientEntry> panel = await ProviderPatients(providerId).GetAllPatientsAsync();
        ProviderPatientEntry panelEntry = panel.Single(p => p.PatientId == patientId);
        Assert.That(panelEntry.NextAppointmentDate, Is.EqualTo(newTime));
    }

    [Test]
    public async Task Reschedule_ToTheSameTime_Succeeds()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(22).AddHours(10);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "SAMETIME CLINIC", apptTime, 30, null, null, "Checkup", "REGULAR");

        await workflow.RescheduleAppointmentAsync(apptId, apptTime, null, "CLERK-2");

        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.AppointmentDateTime, Is.EqualTo(apptTime));
        Assert.That(state.Status, Is.EqualTo("Scheduled"));

        List<AppointmentEntry> entries = await workflow.GetAllAppointmentsAsync();
        Assert.That(entries.Single(e => e.AppointmentId == apptId).Status, Is.EqualTo("Scheduled"));
    }

    [Test]
    public async Task Reschedule_CancelledAppointment_Throws()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(23).AddHours(9);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "CANCELLED-RESCHED CLINIC", apptTime, 30, null, null, null, "REGULAR");
        await workflow.CancelAppointmentWithReasonAsync(apptId, "Patient unavailable", "CLERK-3");

        // A cancelled appointment cannot be rescheduled — the cancellation stands and
        // the caller must book a new appointment instead.
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.RescheduleAppointmentAsync(
                apptId, apptTime.AddDays(1), "Trying to move it", "CLERK-3"));
        Assert.That(ex!.Message, Does.Contain("cancelled"));

        // Nothing moved and nothing was resurrected
        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
        Assert.That(state.AppointmentDateTime, Is.EqualTo(apptTime));

        List<AppointmentEntry> entries = await workflow.GetAllAppointmentsAsync();
        Assert.That(entries.Single(e => e.AppointmentId == apptId).Status, Is.EqualTo("Cancelled"));
    }

    [Test]
    public async Task Reassign_CancelledAppointment_Throws()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(23).AddHours(11);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "CANCELLED-REASSIGN CLINIC", apptTime, 30, null, null, null, "REGULAR");
        await workflow.CancelAppointmentWithReasonAsync(apptId, "Patient unavailable", "CLERK-3");

        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.ReassignAppointmentProviderAsync(
                apptId, $"PROV-{Guid.NewGuid():N}", "LATE,DOC", "Trying to reassign it"));
        Assert.That(ex!.Message, Does.Contain("cancelled"));

        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
        Assert.That(state.ProviderId, Is.Null);
    }

    // ─── CancelAppointmentWithReasonAsync ────────────────────────────────────

    [Test]
    public async Task CancelWithReason_RecordsReasonAndCanceller_AndSyncsIndexes()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        string providerId = $"PROV-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(24).AddHours(11);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "CANCEL-REASON CLINIC", apptTime, 30, providerId, "HOUSE,GREGORY",
            "Consult", "REGULAR");

        await workflow.CancelAppointmentWithReasonAsync(apptId, "Clinic closure", "SUP-1");

        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
        Assert.That(state.CancellationReason, Is.EqualTo("Clinic closure"));
        Assert.That(state.ModifiedBy, Is.EqualTo("SUP-1"));
        Assert.That(state.CancellationDateTime, Is.Not.Null);

        // Patient schedule index and provider schedule both see the cancellation
        List<AppointmentEntry> entries = await workflow.GetAllAppointmentsAsync();
        Assert.That(entries.Single(e => e.AppointmentId == apptId).Status, Is.EqualTo("Cancelled"));

        List<ProviderScheduleEntry> provEntries = await ProviderSchedule(providerId).GetAllAsync();
        Assert.That(provEntries.Single(e => e.AppointmentId == apptId).Status, Is.EqualTo("Cancelled"));

        // Care team membership is NOT removed on cancellation
        Assert.That(await CareTeam(patientId).HasMemberAsync(providerId), Is.True);
    }

    [Test]
    public async Task CancelWithReason_FreesTheSlot_ForANewBooking()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        var clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync("ONE-SLOT CLINIC", null, null, null, null, 30, 1, false, null, null, null);

        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(25).AddHours(9);

        string first = await workflow.ScheduleAppointmentAsync(
            clinicId, "ONE-SLOT CLINIC", apptTime, 30, null, null, null, "REGULAR");
        await workflow.CancelAppointmentWithReasonAsync(first, "No longer needed", "CLERK-4");

        // The cancelled entry no longer counts against capacity or overlap
        string second = await workflow.ScheduleAppointmentAsync(
            clinicId, "ONE-SLOT CLINIC", apptTime, 30, null, null, null, "REGULAR");
        Assert.That(second, Is.Not.EqualTo(first).And.StartsWith("APPT-"));
    }

    [Test]
    public async Task CancelWithReason_Twice_SecondCallIsANoOp_FirstReasonStands()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(26).AddHours(13);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "DOUBLE-CANCEL CLINIC", apptTime, 30, null, null, null, "REGULAR");

        await workflow.CancelAppointmentWithReasonAsync(apptId, "First reason", "CLERK-5");
        await workflow.CancelAppointmentWithReasonAsync(apptId, "Second reason", "CLERK-6");

        // The appointment grain guards against double cancellation: the second call
        // must not overwrite the original audit trail.
        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
        Assert.That(state.CancellationReason, Is.EqualTo("First reason"));
        Assert.That(state.ModifiedBy, Is.EqualTo("CLERK-5"));
    }

    [Test]
    public async Task CancelWithReason_CheckedInAppointment_IsCancelled()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(27).AddHours(8);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "CHECKIN-CANCEL CLINIC", apptTime, 30, null, null, null, "REGULAR");
        await workflow.CheckInAsync(apptId, null);

        AppointmentState checkedIn = await workflow.GetAppointmentAsync(apptId);
        Assert.That(checkedIn.Status, Is.EqualTo("Checked In"));

        // The appointment grain only refuses to cancel an already-cancelled appointment;
        // a checked-in one can still be cancelled (e.g. patient checked in, then left
        // before being seen). This pins that behavior; the check-in timestamp survives.
        await workflow.CancelAppointmentWithReasonAsync(apptId, "Patient left before visit", "NURSE-1");

        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient left before visit"));
        Assert.That(state.CheckInDateTime, Is.Not.Null);
    }

    [Test]
    public async Task CancelWithReason_AutoOffer_BooksARealAppointmentForTheWaitListedPatient()
    {
        // The auto-offer fired on cancellation must reference a REAL appointment
        // (mirroring the staff flow, which pre-books then offers) — not a phantom
        // "AUTO-{guid}" id that never existed.
        await SiteParams().EnableFeatureAsync("APPOINTMENT_WAITLIST");
        try
        {
            string cancellingPatient = await NewPatientAsync();
            string waitingPatient = await NewPatientAsync();
            string clinicId = $"CLINIC-{Guid.NewGuid():N}";
            IPatientWorkflowGrain cancellingWorkflow = Workflow(cancellingPatient);
            IPatientWorkflowGrain waitingWorkflow = Workflow(waitingPatient);

            DateTime apptTime = DateTime.UtcNow.Date.AddDays(35).AddHours(10);

            AppointmentWaitListState wlEntry = await waitingWorkflow.AddToWaitListAsync(
                clinicId, "AUTO-OFFER CLINIC", "FOLLOW-UP", null, null, "ROUTINE",
                null, null, null, "PROV-1", "REFERRING,DOC");

            string cancelledAppt = await cancellingWorkflow.ScheduleAppointmentAsync(
                clinicId, "AUTO-OFFER CLINIC", apptTime, 30, null, null, "Checkup", "REGULAR");
            await cancellingWorkflow.CancelAppointmentWithReasonAsync(
                cancelledAppt, "Patient moved", "CLERK-7");

            AppointmentWaitListState offered = await waitingWorkflow.GetWaitListEntryAsync(wlEntry.EntryId);
            Assert.That(offered.Status, Is.EqualTo("OFFERED"));
            Assert.That(offered.OfferedAppointmentId, Is.Not.Null.And.Not.Empty);
            Assert.That(offered.OfferedAppointmentId, Does.StartWith("APPT-"),
                "the offer must carry a real appointment id, not a phantom AUTO- id");
            Assert.That(offered.OfferedDateTime, Is.EqualTo(apptTime));

            // The offered id exists as a real, scheduled appointment for the wait-listed
            // patient at the freed slot's clinic and time.
            AppointmentState booked = await waitingWorkflow.GetAppointmentAsync(offered.OfferedAppointmentId!);
            Assert.That(booked.PatientId, Is.EqualTo(waitingPatient));
            Assert.That(booked.ClinicId, Is.EqualTo(clinicId));
            Assert.That(booked.AppointmentDateTime, Is.EqualTo(apptTime));
            Assert.That(booked.DurationMinutes, Is.EqualTo(30));
            Assert.That(booked.Status, Is.EqualTo("Scheduled"));
        }
        finally
        {
            await SiteParams().DisableFeatureAsync("APPOINTMENT_WAITLIST");
        }
    }

    // ─── ReassignAppointmentProviderAsync ────────────────────────────────────

    [Test]
    public async Task ReassignProvider_UpdatesAppointment_SchedulesAndPanels()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        string oldProviderId = $"PROV-{Guid.NewGuid():N}";
        string newProviderId = $"PROV-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(28).AddHours(10);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "REASSIGN CLINIC", apptTime, 30, oldProviderId, "OLD,DOC",
            "Consult", "REGULAR");

        await workflow.ReassignAppointmentProviderAsync(
            apptId, newProviderId, "NEW,DOC", "Original provider on leave");

        // The appointment itself carries the new provider
        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.ProviderId, Is.EqualTo(newProviderId));
        Assert.That(state.ProviderName, Is.EqualTo("NEW,DOC"));
        Assert.That(state.Status, Is.EqualTo("Scheduled"));

        // The clinical Purpose survives the reassignment — the reason is audit trail
        // in Notes, never a replacement for Purpose.
        Assert.That(state.Purpose, Is.EqualTo("Consult"));
        Assert.That(state.Notes, Does.Contain("Original provider on leave"));

        // Patient schedule index sees the new provider
        List<AppointmentEntry> entries = await workflow.GetAllAppointmentsAsync();
        AppointmentEntry entry = entries.Single(e => e.AppointmentId == apptId);
        Assert.That(entry.ProviderId, Is.EqualTo(newProviderId));
        Assert.That(entry.ProviderName, Is.EqualTo("NEW,DOC"));

        // Old provider's schedule no longer lists the appointment; the new one does
        List<ProviderScheduleEntry> oldSched = await ProviderSchedule(oldProviderId).GetAllAsync();
        Assert.That(oldSched.Select(e => e.AppointmentId), Does.Not.Contain(apptId));

        List<ProviderScheduleEntry> newSched = await ProviderSchedule(newProviderId).GetAllAsync();
        ProviderScheduleEntry newEntry = newSched.Single(e => e.AppointmentId == apptId);
        Assert.That(newEntry.PatientId, Is.EqualTo(patientId));
        Assert.That(newEntry.AppointmentDateTime, Is.EqualTo(apptTime));

        // Care team: new provider added; old provider membership is NOT removed
        Assert.That(await CareTeam(patientId).HasActiveMemberAsync(newProviderId), Is.True);
        Assert.That(await CareTeam(patientId).HasMemberAsync(oldProviderId), Is.True);

        // New provider's patient panel gains this patient with the appointment date
        List<ProviderPatientEntry> panel = await ProviderPatients(newProviderId).GetAllPatientsAsync();
        ProviderPatientEntry panelEntry = panel.Single(p => p.PatientId == patientId);
        Assert.That(panelEntry.NextAppointmentDate, Is.EqualTo(apptTime));
        Assert.That(panelEntry.Relationship, Is.EqualTo("SPECIALIST"));
    }

    [Test]
    public async Task ReassignProvider_OnAnUnassignedAppointment_AddsTheProvider()
    {
        string patientId = await NewPatientAsync();
        string clinicId = $"CLINIC-{Guid.NewGuid():N}";
        string providerId = $"PROV-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(29).AddHours(15);

        // Scheduled with no provider at all
        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "NO-PROV CLINIC", apptTime, 30, null, null, null, "REGULAR");

        await workflow.ReassignAppointmentProviderAsync(apptId, providerId, "ASSIGNED,DOC", null);

        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.ProviderId, Is.EqualTo(providerId));
        Assert.That(state.ProviderName, Is.EqualTo("ASSIGNED,DOC"));

        List<ProviderScheduleEntry> sched = await ProviderSchedule(providerId).GetAllAsync();
        Assert.That(sched.Select(e => e.AppointmentId), Does.Contain(apptId));
        Assert.That(await CareTeam(patientId).HasActiveMemberAsync(providerId), Is.True);
    }

    // ─── Recall: contact attempts ────────────────────────────────────────────

    private async Task<PatientRecallState> CreateRecallAsync(IPatientWorkflowGrain workflow)
    {
        return await workflow.CreateRecallEntryAsync(
            $"CLINIC-{Guid.NewGuid():N}", "Primary Care",
            "FOLLOW-UP", DateTime.UtcNow.Date.AddMonths(6),
            null, null, "Hypertension follow-up", "Recheck BP",
            "PROV-1", "Dr. Recall");
    }

    [Test]
    public async Task Recall_RecordContactAttempts_CountGrows_AndReachedMarksContacted()
    {
        string patientId = await NewPatientAsync();
        IPatientWorkflowGrain workflow = Workflow(patientId);
        PatientRecallState entry = await CreateRecallAsync(workflow);
        Assert.That(entry.Status, Is.EqualTo("PENDING"));

        await workflow.RecordRecallContactAttemptAsync(
            entry.EntryId, "PHONE", "NO_ANSWER", "Clerk Adams", "Left voicemail");

        PatientRecallState afterFirst = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(afterFirst.ContactAttemptCount, Is.EqualTo(1));
        Assert.That(afterFirst.ContactAttempts, Has.Count.EqualTo(1));
        Assert.That(afterFirst.Status, Is.EqualTo("PENDING"), "an unreached attempt does not change status");
        Assert.That(afterFirst.ContactAttempts[0].ContactMethod, Is.EqualTo("PHONE"));
        Assert.That(afterFirst.ContactAttempts[0].Result, Is.EqualTo("NO_ANSWER"));
        Assert.That(afterFirst.ContactAttempts[0].ContactedByName, Is.EqualTo("Clerk Adams"));
        Assert.That(afterFirst.ContactAttempts[0].Notes, Is.EqualTo("Left voicemail"));

        await workflow.RecordRecallContactAttemptAsync(
            entry.EntryId, "PHONE", "REACHED", "Clerk Adams", null);

        PatientRecallState afterSecond = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(afterSecond.ContactAttemptCount, Is.EqualTo(2));
        Assert.That(afterSecond.ContactAttempts, Has.Count.EqualTo(2));
        Assert.That(afterSecond.Status, Is.EqualTo("CONTACTED"));

        // The recall index mirrors the attempt count and status
        List<PatientRecallIndexEntry> indexEntries = await workflow.GetRecallEntriesAsync();
        PatientRecallIndexEntry idx = indexEntries.Single(e => e.EntryId == entry.EntryId);
        Assert.That(idx.ContactAttemptCount, Is.EqualTo(2));
        Assert.That(idx.Status, Is.EqualTo("CONTACTED"));
    }

    // ─── Recall: scheduling the appointment closes/links the entry ───────────

    [Test]
    public async Task Recall_ScheduleRecallAppointment_LinksAppointmentAndClosesEntry()
    {
        string patientId = await NewPatientAsync();
        IPatientWorkflowGrain workflow = Workflow(patientId);
        PatientRecallState entry = await CreateRecallAsync(workflow);

        // Book a real appointment, then link it to the recall entry
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(30).AddHours(9);
        string apptId = await workflow.ScheduleAppointmentAsync(
            $"CLINIC-{Guid.NewGuid():N}", "RECALL CLINIC", apptTime, 30,
            null, null, "Recall follow-up", "REGULAR");

        await workflow.ScheduleRecallAppointmentAsync(entry.EntryId, apptId, apptTime, "Clerk Baker");

        PatientRecallState linked = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(linked.Status, Is.EqualTo("APPOINTMENT_SCHEDULED"));
        Assert.That(linked.ScheduledAppointmentId, Is.EqualTo(apptId));
        Assert.That(linked.ScheduledAppointmentDateTime, Is.EqualTo(apptTime));
        Assert.That(linked.ScheduledByName, Is.EqualTo("Clerk Baker"));

        List<PatientRecallIndexEntry> indexEntries = await workflow.GetRecallEntriesAsync();
        Assert.That(indexEntries.Single(e => e.EntryId == entry.EntryId).Status,
            Is.EqualTo("APPOINTMENT_SCHEDULED"));
    }

    // ─── Recall: cancellation ────────────────────────────────────────────────

    [Test]
    public async Task Recall_CancelEntry_RecordsReasonAndUpdatesIndex()
    {
        string patientId = await NewPatientAsync();
        IPatientWorkflowGrain workflow = Workflow(patientId);
        PatientRecallState entry = await CreateRecallAsync(workflow);

        await workflow.CancelRecallEntryAsync(entry.EntryId, "Patient moved away", "Clerk Cole");

        PatientRecallState cancelled = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(cancelled.Status, Is.EqualTo("CANCELLED"));
        Assert.That(cancelled.CancellationReason, Is.EqualTo("Patient moved away"));

        List<PatientRecallIndexEntry> indexEntries = await workflow.GetRecallEntriesAsync();
        Assert.That(indexEntries.Single(e => e.EntryId == entry.EntryId).Status,
            Is.EqualTo("CANCELLED"));
    }

    [Test]
    public async Task Recall_CancelTwice_StaysCancelled()
    {
        string patientId = await NewPatientAsync();
        IPatientWorkflowGrain workflow = Workflow(patientId);
        PatientRecallState entry = await CreateRecallAsync(workflow);

        await workflow.CancelRecallEntryAsync(entry.EntryId, "First reason", "Clerk Dean");
        await workflow.CancelRecallEntryAsync(entry.EntryId, "Second reason", "Clerk Dean");

        // The recall grain has no double-cancel guard: the second call succeeds and the
        // reason is last-writer-wins. This pins current behavior — the status never
        // leaves CANCELLED, which is the part that matters clinically.
        PatientRecallState cancelled = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(cancelled.Status, Is.EqualTo("CANCELLED"));
        Assert.That(cancelled.CancellationReason, Is.EqualTo("Second reason"));
    }

    [Test]
    public async Task Recall_ContactAttemptOnCancelledEntry_CountsAttempt_ButStaysCancelled()
    {
        string patientId = await NewPatientAsync();
        IPatientWorkflowGrain workflow = Workflow(patientId);
        PatientRecallState entry = await CreateRecallAsync(workflow);

        await workflow.CancelRecallEntryAsync(entry.EntryId, "Care transferred", "Clerk Ellis");

        // Recording attempts against a cancelled entry is permitted — they are kept for
        // the audit trail — but must never revive the entry. This includes a REACHED
        // result, which only advances entries still awaiting contact (PENDING /
        // LETTER_SENT / OVERDUE), never a cancelled one.
        await workflow.RecordRecallContactAttemptAsync(
            entry.EntryId, "MAIL", "NO_ANSWER", "Clerk Ellis", "Letter returned");
        await workflow.RecordRecallContactAttemptAsync(
            entry.EntryId, "PHONE", "REACHED", "Clerk Ellis", "Patient confirmed care transferred");

        PatientRecallState state = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(state.ContactAttemptCount, Is.EqualTo(2));
        Assert.That(state.ContactAttempts, Has.Count.EqualTo(2));
        Assert.That(state.Status, Is.EqualTo("CANCELLED"),
            "a REACHED attempt must not resurrect a cancelled recall entry");

        List<PatientRecallIndexEntry> indexEntries = await workflow.GetRecallEntriesAsync();
        Assert.That(indexEntries.Single(e => e.EntryId == entry.EntryId).Status,
            Is.EqualTo("CANCELLED"));
    }
}
