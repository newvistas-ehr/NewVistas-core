// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for appointment wait list management with auto-rebooking.
/// Enabled per site via ISiteParametersGrain.Features containing "APPOINTMENT_WAITLIST".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to IHS RPMS SD Wait List functionality where patients are placed on a
/// clinic wait list and automatically offered the next available slot when
/// cancellations or new openings occur.
/// Keyed by wait list entry ID (e.g., "SD-WL:{guid}").
/// </summary>
public interface IAppointmentWaitListGrain : IGrainWithStringKey
{
    Task<AppointmentWaitListState> GetEntryAsync();

    Task<AppointmentWaitListState> CreateEntryAsync(
        string patientId,
        string patientName,
        string clinicId,
        string clinicName,
        string desiredAppointmentType,
        string? preferredProviderId,
        string? preferredProviderName,
        string priority,
        DateTime? desiredDateRangeStart,
        DateTime? desiredDateRangeEnd,
        string? comments,
        string createdByProviderId,
        string createdByProviderName);

    Task UpdatePriorityAsync(string priority);
    Task UpdateDesiredDateRangeAsync(DateTime? rangeStart, DateTime? rangeEnd);
    Task OfferSlotAsync(string appointmentId, DateTime offeredDateTime, string offeredByName);
    Task AcceptOfferAsync(string acceptedByName);
    Task DeclineOfferAsync(string reason, string declinedByName);
    Task BookFromWaitListAsync(string appointmentId, DateTime bookedDateTime, string bookedByName);
    Task CancelEntryAsync(string reason, string cancelledByName);
    Task ExpireEntryAsync();
}

/// <summary>
/// System-level index grain for appointment wait list entries.
/// Singleton keyed by "SD-WL-IDX".
/// Supports queries by clinic, patient, status, and priority.
/// </summary>
public interface IAppointmentWaitListIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(AppointmentWaitListIndexEntry entry);
    Task RemoveAsync(string entryId);
    Task<List<AppointmentWaitListIndexEntry>> GetByClinicAsync(string clinicId, int maxResults = 50);
    Task<List<AppointmentWaitListIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
    Task<List<AppointmentWaitListIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<List<AppointmentWaitListIndexEntry>> GetPendingByClinicAsync(string clinicId, int maxResults = 50);
    Task<List<AppointmentWaitListIndexEntry>> SearchAsync(string? clinicId, string? status, string? priority, int maxResults = 50);
    Task<int> GetCountAsync();

    /// <summary>
    /// Find the highest-priority WAITING entry for a given clinic that matches
    /// the opened slot's date. Returns null if no match.
    /// Used by waitlist auto-offer when a slot opens due to cancellation.
    /// </summary>
    Task<AppointmentWaitListIndexEntry?> FindBestMatchForSlotAsync(string clinicId, DateTime slotDateTime);
}
