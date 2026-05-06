// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
        await grain.AcceptOfferAsync(acceptedByName);
    }

    public async Task DeclineWaitListOfferAsync(string entryId, string reason, string declinedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AppointmentWaitListFeature);
        if (!enabled)
            throw new InvalidOperationException("Appointment wait list is not enabled for this site.");

        IAppointmentWaitListGrain grain = GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);
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
