// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Provider Unavailability Grain — orchestrates the batch processing of appointments
/// when a provider becomes suddenly unavailable (illness, injury, emergency).
///
/// Key pattern: "PROV-UNAVAIL:{guid}" — one grain per unavailability event.
/// VistA File #44.5 (Non-Count Clinic / Clinic Cancel).
/// </summary>
public interface IProviderUnavailabilityGrain : IGrainWithStringKey
{
    /// <summary>Returns the full event state including affected appointment records.</summary>
    Task<ProviderUnavailabilityState> GetEventAsync();

    /// <summary>
    /// Creates an unavailability event. Identifies affected appointments but does not
    /// cancel or reassign them yet (call ExecuteBatchCancellationAsync or
    /// ExecuteBatchReassignmentAsync to take action).
    /// </summary>
    Task<ProviderUnavailabilityState> CreateEventAsync(
        string providerId,
        string providerName,
        DateTime unavailableFrom,
        DateTime unavailableTo,
        string reason,
        string? notes,
        string initiatedByUserId,
        string initiatedByUserName);

    /// <summary>
    /// Executes batch cancellation of all affected appointments.
    /// Sets provider status to UNAVAILABLE, adds time block, cancels appointments,
    /// generates notifications, and offers slots to waitlisted patients.
    /// </summary>
    Task<ProviderUnavailabilityResult> ExecuteBatchCancellationAsync();

    /// <summary>
    /// Executes batch reassignment of all affected appointments to a replacement provider.
    /// </summary>
    Task<ProviderUnavailabilityResult> ExecuteBatchReassignmentAsync(
        string replacementProviderId,
        string replacementProviderName);

    /// <summary>Marks the event as completed.</summary>
    Task CompleteEventAsync();

    /// <summary>Cancels the event (no further action).</summary>
    Task CancelEventAsync(string reason);
}
