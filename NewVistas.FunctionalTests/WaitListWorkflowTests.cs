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
/// Functional tests for the wait-list offer flow (offer → accept/decline) and the
/// patient-portal self-scheduling read paths, all through the PatientWorkflowGrain
/// orchestration layer:
///   - OfferWaitListSlotAsync / AcceptWaitListOfferAsync / DeclineWaitListOfferAsync
///   - PatientJoinWaitListAsync
///   - GetPatientBookableClinicsAsync / GetPatientBookableSlotsAsync
///   - GetAppointmentsWithDetailsAsync
///
/// Maps to IHS RPMS SD Wait List (File #409.3) and the VAOS-inspired patient portal.
/// NonParallelizable — toggles the APPOINTMENT_WAITLIST feature on SITE:DEFAULT.
/// </summary>
[TestFixture, NonParallelizable]
public class WaitListWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string WaitListFeature = "APPOINTMENT_WAITLIST";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    // The flag is off by default on a fresh site (it is not in the pre-seeded
    // Features set) — restore that default so later fixtures see a clean slate.
    [OneTimeTearDown]
    public async Task OneTimeTeardown() =>
        await SiteParams().DisableFeatureAsync(WaitListFeature);

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(WaitListFeature);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private ISiteParametersGrain SiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IAppointmentWaitListIndexGrain WaitListIndex() =>
        _cluster.GrainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");

    private IClinicIndexGrain ClinicIndex() =>
        _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");

    private async Task<string> NewPatientAsync(string name)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain grain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await grain.UpdateDemographicsAsync(name, "M", new DateTime(1975, 6, 1), "123-45-6789");
        return patientId;
    }

    /// <summary>Creates the clinic grain (so PatientJoinWaitListAsync can read its name)
    /// and optionally registers it in the clinic index for portal listing tests.</summary>
    private async Task<string> NewClinicAsync(
        string name, bool addToIndex = false,
        bool acceptsPatientSelfSchedule = false, string status = "ACTIVE")
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IClinicGrain clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync(name, "MAIN", "323", null, null,
            30, 20, false, "C", null, null);

        if (addToIndex)
        {
            await ClinicIndex().AddOrUpdateClinicAsync(new ClinicEntry
            {
                ClinicId = clinicId,
                Name = name,
                Division = "MAIN",
                StopCode = "323",
                AppointmentLength = 30,
                Status = status,
                AcceptsPatientSelfSchedule = acceptsPatientSelfSchedule
            });
        }

        return clinicId;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Offer → accept (full happy path)
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WaitListOffer_JoinOfferAccept_BooksTheOfferedAppointment()
    {
        // Arrange — patient joins the wait list through the portal path
        string patientId = await NewPatientAsync("WAITLIST,ACCEPT A");
        string clinicId = await NewClinicAsync("Offer Accept Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.PatientJoinWaitListAsync(
            clinicId, "FOLLOW-UP", null, null, null, "Soonest available please");
        Assert.That(entry.Status, Is.EqualTo("WAITING"));

        // Staff books a concrete slot for this patient, then offers it against the entry
        DateTime slotTime = DateTime.UtcNow.Date.AddDays(21).AddHours(9);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Offer Accept Clinic", slotTime, 30, null, null,
            "Wait list offer", "FOLLOW-UP");

        // Act — offer, then patient accepts
        await workflow.OfferWaitListSlotAsync(entry.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");

        AppointmentWaitListState offered = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(offered.Status, Is.EqualTo("OFFERED"));
        Assert.That(offered.OfferedAppointmentId, Is.EqualTo(appointmentId));
        Assert.That(offered.OfferedDateTime, Is.EqualTo(slotTime));
        Assert.That(offered.OfferCount, Is.EqualTo(1));

        await workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,ACCEPT A");

        // Assert — the entry is booked against the offered appointment
        AppointmentWaitListState booked = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(booked.Status, Is.EqualTo("BOOKED"));
        Assert.That(booked.BookedAppointmentId, Is.EqualTo(appointmentId));
        Assert.That(booked.BookedDateTime, Is.EqualTo(slotTime));
        Assert.That(booked.BookedByName, Is.EqualTo("WAITLIST,ACCEPT A"));

        // ... the appointment really exists, for this patient, at the offered clinic/time
        AppointmentState appt = await _cluster.GrainFactory
            .GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();
        Assert.That(appt.PatientId, Is.EqualTo(patientId));
        Assert.That(appt.ClinicId, Is.EqualTo(clinicId));
        Assert.That(appt.AppointmentDateTime, Is.EqualTo(slotTime));
        Assert.That(appt.Status, Is.EqualTo("Scheduled"));

        // ... and the entry has left the active (pending) wait list for the clinic
        List<AppointmentWaitListIndexEntry> pending =
            await WaitListIndex().GetPendingByClinicAsync(clinicId);
        Assert.That(pending.Select(p => p.EntryId), Does.Not.Contain(entry.EntryId));

        // The patient's own wait list view still shows the entry, now BOOKED
        List<AppointmentWaitListIndexEntry> mine = await workflow.GetWaitListEntriesAsync();
        AppointmentWaitListIndexEntry mineEntry = mine.Single(e => e.EntryId == entry.EntryId);
        Assert.That(mineEntry.Status, Is.EqualTo("BOOKED"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Offer → decline
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WaitListOffer_Decline_ReturnsEntryToWaitingAndAllowsReOffer()
    {
        // Arrange
        string patientId = await NewPatientAsync("WAITLIST,DECLINE A");
        string clinicId = await NewClinicAsync("Offer Decline Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Offer Decline Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null,
            "PROV-1", "Dr. Jones");

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(22).AddHours(10);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Offer Decline Clinic", slotTime, 30, null, null, null, "FOLLOW-UP");

        await workflow.OfferWaitListSlotAsync(entry.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");

        // Act — patient declines the offer
        await workflow.DeclineWaitListOfferAsync(entry.EntryId, "Time does not work", "WAITLIST,DECLINE A");

        // Assert — the entry returns to WAITING with the offer fields cleared
        AppointmentWaitListState declined = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(declined.Status, Is.EqualTo("WAITING"));
        Assert.That(declined.DeclineReason, Is.EqualTo("Time does not work"));
        Assert.That(declined.OfferedAppointmentId, Is.Null);
        Assert.That(declined.OfferedDateTime, Is.Null);
        Assert.That(declined.OfferedByName, Is.Null);
        Assert.That(declined.BookedAppointmentId, Is.Null);
        Assert.That(declined.OfferCount, Is.EqualTo(1), "the offer history is preserved");

        // ... and it is back on the active (pending) list for the clinic
        List<AppointmentWaitListIndexEntry> pending =
            await WaitListIndex().GetPendingByClinicAsync(clinicId);
        Assert.That(pending.Select(p => p.EntryId), Does.Contain(entry.EntryId));

        // ... the previously offered (pre-booked) appointment was cancelled by the
        // decline, so the slot is freed instead of staying orphaned as "Scheduled"
        AppointmentState declinedAppt = await _cluster.GrainFactory
            .GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();
        Assert.That(declinedAppt.Status, Is.EqualTo("Cancelled"));
        Assert.That(declinedAppt.CancellationReason, Is.EqualTo("Wait-list offer declined"));

        // ... and the slot is bookable again through the portal
        List<AvailableSlot> freed = await workflow.GetPatientBookableSlotsAsync(clinicId, slotTime.Date);
        Assert.That(freed.Select(s => s.StartTime), Does.Contain(slotTime),
            "the declined slot is free again");

        // A fresh slot can be offered again after the decline
        DateTime secondSlot = slotTime.AddDays(1);
        string secondAppointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Offer Decline Clinic", secondSlot, 30, null, null, null, "FOLLOW-UP");
        await workflow.OfferWaitListSlotAsync(entry.EntryId, secondAppointmentId, secondSlot, "SCHEDULER,SUE");

        AppointmentWaitListState reOffered = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(reOffered.Status, Is.EqualTo("OFFERED"));
        Assert.That(reOffered.OfferedAppointmentId, Is.EqualTo(secondAppointmentId));
        Assert.That(reOffered.OfferCount, Is.EqualTo(2));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Stale offers — the offered appointment is gone or belongs to someone else
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WaitListOffer_AcceptAfterOfferedAppointmentCancelled_ThrowsAndReturnsEntryToWaiting()
    {
        // Arrange — offer a real pre-booked slot, then cancel that appointment
        // out from under the offer (e.g. clinic closure) before the patient accepts
        string patientId = await NewPatientAsync("WAITLIST,STALE A");
        string clinicId = await NewClinicAsync("Stale Offer Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Stale Offer Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(26).AddHours(10);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Stale Offer Clinic", slotTime, 30, null, null, null, "FOLLOW-UP");
        await workflow.OfferWaitListSlotAsync(entry.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");

        await workflow.CancelAppointmentAsync(appointmentId);

        // Act & Assert — accepting the now-dead offer is rejected...
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,STALE A"));
        Assert.That(ex!.Message, Does.Contain("no longer available"));

        // ... and the entry was returned to WAITING (offer voided) so staff can re-offer
        AppointmentWaitListState voided = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(voided.Status, Is.EqualTo("WAITING"));
        Assert.That(voided.OfferedAppointmentId, Is.Null);
        Assert.That(voided.BookedAppointmentId, Is.Null);

        // A fresh offer against a new real appointment succeeds
        DateTime secondSlot = slotTime.AddDays(1);
        string secondAppointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Stale Offer Clinic", secondSlot, 30, null, null, null, "FOLLOW-UP");
        await workflow.OfferWaitListSlotAsync(entry.EntryId, secondAppointmentId, secondSlot, "SCHEDULER,SUE");
        await workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,STALE A");

        AppointmentWaitListState booked = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(booked.Status, Is.EqualTo("BOOKED"));
        Assert.That(booked.BookedAppointmentId, Is.EqualTo(secondAppointmentId));
    }

    [Test]
    public async Task WaitListOffer_AcceptWhenAppointmentBelongsToAnotherPatient_ThrowsAndLeavesTheirBookingAlone()
    {
        // Arrange — patient A holds a real Scheduled appointment; patient B's wait
        // list entry is (erroneously) offered that same appointment id
        string patientAId = await NewPatientAsync("WAITLIST,OWNER A");
        string patientBId = await NewPatientAsync("WAITLIST,CLAIMER B");
        string clinicId = await NewClinicAsync("Stolen Slot Clinic");
        IPatientWorkflowGrain workflowA = Workflow(patientAId);
        IPatientWorkflowGrain workflowB = Workflow(patientBId);

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(27).AddHours(9);
        string appointmentId = await workflowA.ScheduleAppointmentAsync(
            clinicId, "Stolen Slot Clinic", slotTime, 30, null, null, null, "FOLLOW-UP");

        AppointmentWaitListState entryB = await workflowB.AddToWaitListAsync(
            clinicId, "Stolen Slot Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");
        await workflowB.OfferWaitListSlotAsync(entryB.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");

        // Act & Assert — B cannot accept an appointment booked for A
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflowB.AcceptWaitListOfferAsync(entryB.EntryId, "WAITLIST,CLAIMER B"));
        Assert.That(ex!.Message, Does.Contain("no longer available"));

        // B's entry is back to WAITING with the bad offer voided
        AppointmentWaitListState voided = await workflowB.GetWaitListEntryAsync(entryB.EntryId);
        Assert.That(voided.Status, Is.EqualTo("WAITING"));
        Assert.That(voided.OfferedAppointmentId, Is.Null);
        Assert.That(voided.BookedAppointmentId, Is.Null);

        // A's appointment is untouched — still Scheduled, still A's
        AppointmentState appt = await _cluster.GrainFactory
            .GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();
        Assert.That(appt.Status, Is.EqualTo("Scheduled"));
        Assert.That(appt.PatientId, Is.EqualTo(patientAId));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Offer status guards — no resurrecting closed entries
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WaitListOffer_OfferOnCancelledEntry_Throws()
    {
        // Arrange — a cancelled wait list entry
        string patientId = await NewPatientAsync("WAITLIST,CANCELLED A");
        string clinicId = await NewClinicAsync("Cancelled Entry Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Cancelled Entry Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");
        await workflow.CancelWaitListEntryAsync(entry.EntryId, "Patient moved away", "Admin");

        // Act & Assert — offering a slot must not resurrect the cancelled entry
        DateTime slotTime = DateTime.UtcNow.Date.AddDays(28).AddHours(9);
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.OfferWaitListSlotAsync(
                entry.EntryId, $"APPT-{Guid.NewGuid()}", slotTime, "SCHEDULER,SUE"));
        Assert.That(ex!.Message, Does.Contain("only WAITING"));

        AppointmentWaitListState state = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.OfferedAppointmentId, Is.Null);
        Assert.That(state.OfferCount, Is.EqualTo(0));
    }

    [Test]
    public async Task WaitListOffer_OfferOnBookedEntry_Throws()
    {
        // Arrange — an entry that has already been booked via offer → accept
        string patientId = await NewPatientAsync("WAITLIST,BOOKEDOFFER A");
        string clinicId = await NewClinicAsync("Booked Offer Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Booked Offer Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(29).AddHours(9);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Booked Offer Clinic", slotTime, 30, null, null, null, "FOLLOW-UP");
        await workflow.OfferWaitListSlotAsync(entry.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");
        await workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,BOOKEDOFFER A");

        // Act & Assert — a BOOKED entry cannot receive another offer
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.OfferWaitListSlotAsync(
                entry.EntryId, $"APPT-{Guid.NewGuid()}", slotTime.AddDays(1), "SCHEDULER,SUE"));
        Assert.That(ex!.Message, Does.Contain("only WAITING"));

        AppointmentWaitListState state = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(state.Status, Is.EqualTo("BOOKED"));
        Assert.That(state.BookedAppointmentId, Is.EqualTo(appointmentId));
    }

    [Test]
    public async Task WaitList_CancelBookedEntry_ThrowsAndLeavesBookingIntact()
    {
        // Arrange — an entry booked via offer → accept
        string patientId = await NewPatientAsync("WAITLIST,BOOKEDCANCEL A");
        string clinicId = await NewClinicAsync("Booked Cancel Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Booked Cancel Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(30).AddHours(10);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Booked Cancel Clinic", slotTime, 30, null, null, null, "FOLLOW-UP");
        await workflow.OfferWaitListSlotAsync(entry.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");
        await workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,BOOKEDCANCEL A");

        // Act & Assert — domain decision: a BOOKED entry is terminal-success; the
        // wait-list cancel is rejected and the caller is pointed at the appointment
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.CancelWaitListEntryAsync(entry.EntryId, "changed mind", "Admin"));
        Assert.That(ex!.Message, Does.Contain("BOOKED"));
        Assert.That(ex.Message, Does.Contain("Cancel the appointment"));

        // The entry and its booked appointment are both untouched
        AppointmentWaitListState state = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(state.Status, Is.EqualTo("BOOKED"));
        Assert.That(state.CancellationReason, Is.Null);

        AppointmentState appt = await _cluster.GrainFactory
            .GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();
        Assert.That(appt.Status, Is.EqualTo("Scheduled"));
        Assert.That(appt.PatientId, Is.EqualTo(patientId));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Invalid accept transitions
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WaitListOffer_AcceptTwice_SecondAcceptThrows()
    {
        // Arrange — offer and accept once
        string patientId = await NewPatientAsync("WAITLIST,DOUBLEACCEPT A");
        string clinicId = await NewClinicAsync("Double Accept Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Double Accept Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(23).AddHours(11);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Double Accept Clinic", slotTime, 30, null, null, null, "FOLLOW-UP");
        await workflow.OfferWaitListSlotAsync(entry.EntryId, appointmentId, slotTime, "SCHEDULER,SUE");
        await workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,DOUBLEACCEPT A");

        // Act & Assert — a second accept of the same (now consumed) offer is rejected
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,DOUBLEACCEPT A"));
        Assert.That(ex!.Message, Does.Contain("No pending offer"));

        // The booking is unchanged
        AppointmentWaitListState booked = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(booked.Status, Is.EqualTo("BOOKED"));
        Assert.That(booked.BookedAppointmentId, Is.EqualTo(appointmentId));
    }

    [Test]
    public async Task WaitListOffer_AcceptAfterDecline_Throws()
    {
        // Arrange — offer then decline
        string patientId = await NewPatientAsync("WAITLIST,LATEACCEPT A");
        string clinicId = await NewClinicAsync("Late Accept Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Late Accept Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        DateTime slotTime = DateTime.UtcNow.Date.AddDays(24).AddHours(9);
        await workflow.OfferWaitListSlotAsync(entry.EntryId, $"APPT-{Guid.NewGuid()}", slotTime, "SCHEDULER,SUE");
        await workflow.DeclineWaitListOfferAsync(entry.EntryId, "Changed my mind", "WAITLIST,LATEACCEPT A");

        // Act & Assert — the declined offer is gone; accepting it now is rejected
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,LATEACCEPT A"));
        Assert.That(ex!.Message, Does.Contain("No pending offer"));

        AppointmentWaitListState state = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(state.Status, Is.EqualTo("WAITING"));
        Assert.That(state.BookedAppointmentId, Is.Null);
    }

    [Test]
    public async Task WaitListOffer_AcceptWithoutAnyOffer_Throws()
    {
        // Arrange — an entry that is WAITING and has never been offered anything
        string patientId = await NewPatientAsync("WAITLIST,NOOFFER A");
        string clinicId = await NewClinicAsync("No Offer Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "No Offer Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        // Act & Assert — accepting an offer that was never made is rejected
        Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,NOOFFER A"));

        // Same for declining
        Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.DeclineWaitListOfferAsync(entry.EntryId, "n/a", "WAITLIST,NOOFFER A"));

        AppointmentWaitListState state = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(state.Status, Is.EqualTo("WAITING"));

        // An entry id that was never created behaves the same way (default state is WAITING)
        Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.AcceptWaitListOfferAsync($"SD-WL:{Guid.NewGuid()}", "WAITLIST,NOOFFER A"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Feature gate on the offer flow
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WaitListOffer_FeatureDisabled_OfferAcceptDeclineAllThrow()
    {
        // Arrange — create an entry while the feature is on, then turn it off
        string patientId = await NewPatientAsync("WAITLIST,GATED A");
        string clinicId = await NewClinicAsync("Gated Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            clinicId, "Gated Clinic", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null, "PROV-1", "Dr. Jones");

        await SiteParams().DisableFeatureAsync(WaitListFeature);

        // Act & Assert — every leg of the offer flow is closed
        DateTime slotTime = DateTime.UtcNow.Date.AddDays(25).AddHours(9);

        InvalidOperationException? offerEx = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.OfferWaitListSlotAsync(entry.EntryId, $"APPT-{Guid.NewGuid()}", slotTime, "SCHEDULER,SUE"));
        Assert.That(offerEx!.Message, Does.Contain("not enabled"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.AcceptWaitListOfferAsync(entry.EntryId, "WAITLIST,GATED A"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.DeclineWaitListOfferAsync(entry.EntryId, "n/a", "WAITLIST,GATED A"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PatientJoinWaitListAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task PatientJoinWaitList_CreatesRoutineEntryAttributedToThePatient()
    {
        // Arrange
        string patientId = await NewPatientAsync("PORTAL,JOINER A");
        string clinicId = await NewClinicAsync("Portal Join Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        DateTime rangeStart = DateTime.UtcNow.Date.AddDays(7);
        DateTime rangeEnd = DateTime.UtcNow.Date.AddDays(60);

        // Act
        AppointmentWaitListState entry = await workflow.PatientJoinWaitListAsync(
            clinicId, "NEW PATIENT", null, rangeStart, rangeEnd, "Mornings preferred");

        // Assert — patients always join at ROUTINE priority, attributed to themselves,
        // with the clinic name resolved from the clinic grain
        Assert.That(entry.EntryId, Does.StartWith("SD-WL:"));
        Assert.That(entry.Status, Is.EqualTo("WAITING"));
        Assert.That(entry.Priority, Is.EqualTo("ROUTINE"));
        Assert.That(entry.PatientId, Is.EqualTo(patientId));
        Assert.That(entry.PatientName, Is.EqualTo("PORTAL,JOINER A"));
        Assert.That(entry.ClinicId, Is.EqualTo(clinicId));
        Assert.That(entry.ClinicName, Is.EqualTo("Portal Join Clinic"));
        Assert.That(entry.CreatedByProviderId, Is.EqualTo($"PATIENT:{patientId}"));
        Assert.That(entry.CreatedByProviderName, Is.EqualTo("PORTAL,JOINER A"));
        Assert.That(entry.DesiredAppointmentType, Is.EqualTo("NEW PATIENT"));
        Assert.That(entry.DesiredDateRangeStart, Is.EqualTo(rangeStart));
        Assert.That(entry.DesiredDateRangeEnd, Is.EqualTo(rangeEnd));
        Assert.That(entry.Comments, Is.EqualTo("Mornings preferred"));

        // The entry is visible on the patient's wait list and the clinic's pending queue
        List<AppointmentWaitListIndexEntry> mine = await workflow.GetWaitListEntriesAsync();
        Assert.That(mine.Select(e => e.EntryId), Does.Contain(entry.EntryId));

        List<AppointmentWaitListIndexEntry> pending =
            await WaitListIndex().GetPendingByClinicAsync(clinicId);
        Assert.That(pending.Select(e => e.EntryId), Does.Contain(entry.EntryId));
    }

    [Test]
    public async Task PatientJoinWaitList_FeatureDisabled_Throws()
    {
        // Arrange
        string patientId = await NewPatientAsync("PORTAL,GATEDJOIN A");
        string clinicId = await NewClinicAsync("Gated Join Clinic");
        await SiteParams().DisableFeatureAsync(WaitListFeature);

        // Act & Assert — the portal join rides the same APPOINTMENT_WAITLIST gate
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => Workflow(patientId).PatientJoinWaitListAsync(
                clinicId, "FOLLOW-UP", null, null, null, null));
        Assert.That(ex!.Message, Does.Contain("not enabled"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Patient-portal bookable clinics and slots
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task PatientPortal_BookableClinics_OnlyActiveSelfScheduleClinicsAppear()
    {
        // Arrange — three clinics in the shared index with distinct configurations
        string bookableId = await NewClinicAsync("Portal Bookable Clinic",
            addToIndex: true, acceptsPatientSelfSchedule: true, status: "ACTIVE");
        string staffOnlyId = await NewClinicAsync("Portal Staff Only Clinic",
            addToIndex: true, acceptsPatientSelfSchedule: false, status: "ACTIVE");
        string inactiveId = await NewClinicAsync("Portal Inactive Clinic",
            addToIndex: true, acceptsPatientSelfSchedule: true, status: "INACTIVE");

        string patientId = await NewPatientAsync("PORTAL,BROWSER A");

        // Act
        List<ClinicEntry> bookable = await Workflow(patientId).GetPatientBookableClinicsAsync();

        // Assert — the index is shared, so assert membership rather than counts
        List<string> ids = bookable.Select(c => c.ClinicId).ToList();
        Assert.That(ids, Does.Contain(bookableId));
        Assert.That(ids, Does.Not.Contain(staffOnlyId));
        Assert.That(ids, Does.Not.Contain(inactiveId));
        Assert.That(bookable, Is.All.Matches<ClinicEntry>(
            c => c.AcceptsPatientSelfSchedule && c.Status == "ACTIVE"));
    }

    [Test]
    public async Task PatientPortal_BookableSlots_ReflectExistingBookings()
    {
        // Arrange — a fresh clinic (no primary provider → clinic-wide 8-17 grid)
        string patientId = await NewPatientAsync("PORTAL,SLOTS A");
        string clinicId = await NewClinicAsync("Portal Slots Clinic");
        IPatientWorkflowGrain workflow = Workflow(patientId);
        DateTime date = DateTime.UtcNow.Date.AddDays(30);

        // Act 1 — empty day: full 8-17 grid of 30-minute slots
        List<AvailableSlot> before = await workflow.GetPatientBookableSlotsAsync(clinicId, date);

        // Assert 1
        Assert.That(before, Has.Count.EqualTo(18), "8:00-17:00 at 30 minutes = 18 slots");
        Assert.That(before[0].StartTime.Hour, Is.EqualTo(8));
        Assert.That(before, Is.All.Matches<AvailableSlot>(s => s.IsAvailable));

        // Act 2 — book 09:00, then re-query
        DateTime bookedSlot = date.AddHours(9);
        await workflow.ScheduleAppointmentAsync(
            clinicId, "Portal Slots Clinic", bookedSlot, 30, null, null, null, "REGULAR");
        List<AvailableSlot> after = await workflow.GetPatientBookableSlotsAsync(clinicId, date);

        // Assert 2 — the booked slot has disappeared from the bookable list
        Assert.That(after, Has.Count.EqualTo(17));
        Assert.That(after.Select(s => s.StartTime), Does.Not.Contain(bookedSlot));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetAppointmentsWithDetailsAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task PatientPortal_AppointmentsWithDetails_ReturnFullAppointmentState()
    {
        // Arrange — a fresh patient with two appointments at different clinics
        string patientId = await NewPatientAsync("PORTAL,DETAILS A");
        string clinicA = await NewClinicAsync("Details Clinic A");
        string clinicB = await NewClinicAsync("Details Clinic B");
        IPatientWorkflowGrain workflow = Workflow(patientId);

        DateTime timeA = DateTime.UtcNow.Date.AddDays(31).AddHours(9);
        DateTime timeB = DateTime.UtcNow.Date.AddDays(32).AddHours(14);
        string apptA = await workflow.ScheduleAppointmentAsync(
            clinicA, "Details Clinic A", timeA, 30, null, null, "Checkup", "REGULAR");
        string apptB = await workflow.ScheduleAppointmentAsync(
            clinicB, "Details Clinic B", timeB, 30, null, null, "Lab review", "FOLLOW-UP");

        // Act
        List<AppointmentState> details = await workflow.GetAppointmentsWithDetailsAsync();

        // Assert — full state (not just index entries) for both appointments
        Assert.That(details, Has.Count.EqualTo(2));
        Assert.That(details, Is.All.Matches<AppointmentState>(
            d => d.PatientId == patientId && d.Status == "Scheduled"));

        AppointmentState detailA = details.Single(d => d.AppointmentId == apptA);
        Assert.That(detailA.ClinicId, Is.EqualTo(clinicA));
        Assert.That(detailA.ClinicName, Is.EqualTo("Details Clinic A"));
        Assert.That(detailA.AppointmentDateTime, Is.EqualTo(timeA));
        Assert.That(detailA.Purpose, Is.EqualTo("Checkup"));

        AppointmentState detailB = details.Single(d => d.AppointmentId == apptB);
        Assert.That(detailB.ClinicId, Is.EqualTo(clinicB));
        Assert.That(detailB.AppointmentDateTime, Is.EqualTo(timeB));
        Assert.That(detailB.AppointmentType, Is.EqualTo("FOLLOW-UP"));
    }
}
