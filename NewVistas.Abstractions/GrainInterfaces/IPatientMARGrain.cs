// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient Medication Administration Record (MAR) grain.
/// Aggregates active inpatient orders and their full administration history
/// to give bedside nurses a single queryable view of what is due, given, or held.
///
/// Key format: "BCMA-MAR:{patientId}"
/// Store:      bcmaMarStore
/// Maps to VistA PSB (BCMA) package — MEDICATION LOG file #53.79.
/// </summary>
public interface IPatientMARGrain : IGrainWithStringKey
{
    /// <summary>Return all MAR entries, ordered by priority then drug name.</summary>
    Task<List<MarEntry>> GetMARAsync();

    /// <summary>Return only entries where <see cref="MarEntry.IsActive"/> is true.</summary>
    Task<List<MarEntry>> GetActiveMARAsync();

    /// <summary>
    /// Return active entries that have at least one scheduled time at or before
    /// (now + 60 minutes), i.e. overdue or due within the hour.
    /// </summary>
    Task<List<MarEntry>> GetDueNowAsync();

    /// <summary>Upsert a MAR entry by <see cref="MarEntry.OrderId"/>.</summary>
    Task AddOrUpdateEntryAsync(MarEntry entry);

    /// <summary>
    /// Set <see cref="MarEntry.IsActive"/> to false for the given order.
    /// Called when the underlying inpatient order is discontinued or expires.
    /// </summary>
    Task DeactivateEntryAsync(string orderId);

    /// <summary>
    /// Record a dose administration event against a specific order.
    /// Updates <see cref="MarEntry.LastAdminDateTime"/>, <see cref="MarEntry.LastAdminStatus"/>,
    /// <see cref="MarEntry.LastAdminBy"/>, <see cref="MarEntry.TotalAdministrations"/>,
    /// and appends to <see cref="MarEntry.BcmaAdministrationIds"/>.
    /// </summary>
    Task RecordAdministrationAsync(
        string orderId,
        string bcmaId,
        string actionStatus,
        DateTime adminTime,
        string? adminByName);
}
