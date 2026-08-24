// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Appointment Wait List — Site Flavor Architecture (Option 4: Composition).
/// Checks the APPOINTMENT_WAITLIST feature flag before delegating to
/// the optional IAppointmentWaitListGrain. If the feature is not enabled,
/// the wait list grains are never activated and consume no resources.
///
/// Maps to IHS RPMS SD Wait List — auto-rebooking from wait list.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string AppointmentWaitListFeature = "APPOINTMENT_WAITLIST";

    public async Task<AppointmentWaitListState> AddToWaitListAsync(
        string clinicId, string clinicName,
        string desiredAppointmentType,
        string? preferredProviderId, string? preferredProviderName,
        string priority,
        DateTime? desiredDateRangeStart, DateTime? desiredDateRangeEnd,
        string? comments,
        string createdByProviderId, string createdByProviderName)
    {
        // ── Feature gate ────────────────────────────────────────────
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Appointment wait list is not enabled for this site. Enable the APPOINTMENT_WAITLIST feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string entryId = $"SD-WL:{Guid.NewGuid()}";
        IAppointmentWaitListGrain grain =
            GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);

        AppointmentWaitListState result = await grain.CreateEntryAsync(
            PatientId, patient.Name,
            clinicId, clinicName,
            desiredAppointmentType,
            preferredProviderId, preferredProviderName,
            priority,
            desiredDateRangeStart, desiredDateRangeEnd,
            comments,
            createdByProviderId, createdByProviderName);

        await LogAuditEventAsync(
            "SCHEDULING", "ADD_TO_WAITLIST", "WaitList", entryId,
            createdByProviderId, createdByProviderName, null, null,
            $"Added to wait list for {clinicName}, priority {priority}");

        return result;
    }

    public async Task<List<AppointmentWaitListIndexEntry>> GetWaitListEntriesAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled) return [];

        IAppointmentWaitListIndexGrain index =
            GrainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<AppointmentWaitListState> GetWaitListEntryAsync(string entryId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException("Appointment wait list is not enabled for this site.");

        IAppointmentWaitListGrain grain = GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);
        return await grain.GetEntryAsync();
    }

    public async Task OfferWaitListSlotAsync(
        string entryId, string appointmentId, DateTime offeredDateTime, string offeredByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException("Appointment wait list is not enabled for this site.");

        IAppointmentWaitListGrain grain = GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);
        await grain.OfferSlotAsync(appointmentId, offeredDateTime, offeredByName);
    }

    public async Task AcceptWaitListOfferAsync(string entryId, string acceptedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException("Appointment wait list is not enabled for this site.");

        IAppointmentWaitListGrain grain = GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);
        AppointmentWaitListState entry = await grain.GetEntryAsync();

        if (entry.Status != "OFFERED" || string.IsNullOrEmpty(entry.OfferedAppointmentId))
            throw new InvalidOperationException("No pending offer to accept.");

        // The staff offer flow pre-books the offered appointment for THIS patient, so
        // at accept time it must still exist, still be "Scheduled", and still belong
        // to this patient. If two entries were somehow offered the same appointment,
        // only the patient the appointment is actually booked for can accept it; if
        // the appointment was cancelled or rebooked in the meantime, nobody can.
        // On a stale offer the entry is returned to WAITING (offer voided) so staff
        // can re-offer a fresh slot, and the accept is rejected.
        IAppointmentGrain apptGrain =
            GrainFactory.GetGrain<IAppointmentGrain>(entry.OfferedAppointmentId);
        AppointmentState appt = await apptGrain.GetAppointmentAsync();
        bool apptExists = !string.IsNullOrEmpty(appt.PatientId);

        if (!apptExists || appt.Status != "Scheduled" || appt.PatientId != PatientId)
        {
            await grain.VoidOfferAsync(
                $"Offered appointment {entry.OfferedAppointmentId} was no longer available at accept time",
                acceptedByName);
            throw new InvalidOperationException(
                $"Offered appointment {entry.OfferedAppointmentId} is no longer available " +
                "(cancelled, rebooked, or assigned to another patient); the wait-list entry " +
                "has been returned to WAITING so a new slot can be offered.");
        }

        await grain.AcceptOfferAsync(acceptedByName);
    }

    public async Task DeclineWaitListOfferAsync(string entryId, string reason, string declinedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException("Appointment wait list is not enabled for this site.");

        IAppointmentWaitListGrain grain = GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);
        AppointmentWaitListState entry = await grain.GetEntryAsync();

        if (entry.Status != "OFFERED")
            throw new InvalidOperationException("No pending offer to decline.");

        // The offer pre-booked a real appointment for this patient — free that slot,
        // or the declined booking is orphaned ("Scheduled" forever for a patient who
        // said no). Cancel BEFORE the entry returns to WAITING so the auto-offer
        // triggered by the cancellation cannot hand the just-declined slot straight
        // back to this same entry. Skipped when the offered appointment id doesn't
        // exist (pre-fix offers carried synthetic ids), already left "Scheduled",
        // or belongs to a different patient — never cancel someone else's booking.
        if (!string.IsNullOrEmpty(entry.OfferedAppointmentId))
        {
            IAppointmentGrain apptGrain =
                GrainFactory.GetGrain<IAppointmentGrain>(entry.OfferedAppointmentId);
            AppointmentState appt = await apptGrain.GetAppointmentAsync();

            if (!string.IsNullOrEmpty(appt.PatientId)
                && appt.PatientId == PatientId
                && appt.Status == "Scheduled")
            {
                await CancelAppointmentWithReasonAsync(
                    entry.OfferedAppointmentId, "Wait-list offer declined", declinedByName);
            }
        }

        await grain.DeclineOfferAsync(reason, declinedByName);
    }

    public async Task CancelWaitListEntryAsync(string entryId, string reason, string cancelledByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException("Appointment wait list is not enabled for this site.");

        IAppointmentWaitListGrain grain = GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);
        await grain.CancelEntryAsync(reason, cancelledByName);
    }
}
