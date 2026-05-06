// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Provider Availability Grain — defines when a provider works at which clinic
/// and manages time blocks (vacation, sick leave, admin time, etc.).
///
/// Key pattern: "PROV-AVAIL:{providerId}"
/// VistA File #44.005 (SD Clinic Availability), File #44.002 (Provider),
/// File #44.003 (Appointment Type).
/// MUMPS references: SDCOU.m, SDBUILD.m
/// </summary>
public interface IProviderAvailabilityGrain : IGrainWithStringKey
{
    // ─── State retrieval ─────────────────────────────────────────────

    /// <summary>
    /// Returns the full provider availability state.
    /// </summary>
    Task<ProviderAvailabilityState> GetAvailabilityAsync();

    // ─── Provider status ─────────────────────────────────────────────

    /// <summary>
    /// Updates the provider's scheduling status (ACTIVE, ON_LEAVE, UNAVAILABLE).
    /// </summary>
    Task UpdateProviderStatusAsync(string status, string? reason, string? modifiedBy);

    // ─── Recurring weekly patterns ───────────────────────────────────

    /// <summary>
    /// Adds a recurring weekly availability pattern for a clinic.
    /// E.g., "Mon/Wed/Fri 8:00-12:00 at PRIMARY CARE".
    /// </summary>
    Task AddWeeklyPatternAsync(WeeklyAvailabilityPattern pattern);

    /// <summary>
    /// Updates an existing weekly pattern by its PatternId.
    /// </summary>
    Task UpdateWeeklyPatternAsync(string patternId, WeeklyAvailabilityPattern pattern);

    /// <summary>
    /// Removes a weekly pattern by its PatternId.
    /// </summary>
    Task RemoveWeeklyPatternAsync(string patternId);

    // ─── One-off time blocks ─────────────────────────────────────────

    /// <summary>
    /// Adds a time block (vacation, sick leave, lunch, admin, meeting, etc.).
    /// Returns the generated block ID.
    /// </summary>
    Task<string> AddTimeBlockAsync(ProviderTimeBlock block);

    /// <summary>
    /// Updates an existing time block by its BlockId.
    /// </summary>
    Task UpdateTimeBlockAsync(string blockId, ProviderTimeBlock block);

    /// <summary>
    /// Removes a time block by its BlockId.
    /// </summary>
    Task RemoveTimeBlockAsync(string blockId);

    /// <summary>
    /// Returns all time blocks that overlap with the given date range.
    /// </summary>
    Task<List<ProviderTimeBlock>> GetTimeBlocksForDateRangeAsync(DateTime start, DateTime end);

    // ─── Computed availability for scheduling ────────────────────────

    /// <summary>
    /// Returns the effective availability windows for a provider at a specific clinic
    /// on a given date, after applying weekly patterns and subtracting time blocks.
    /// This is the primary method called by slot generation.
    /// </summary>
    Task<List<AvailabilityWindow>> GetEffectiveAvailabilityAsync(string clinicId, DateTime date);

    /// <summary>
    /// Returns all clinics where this provider has availability on the given date.
    /// Used by "find available providers" searches.
    /// </summary>
    Task<List<ProviderClinicAvailabilitySummary>> GetAvailableClinicsForDateAsync(DateTime date);

    // ─── Scheduling tier configuration ───────────────────────────────

    /// <summary>
    /// Sets the scheduling tier configuration for this provider at a specific clinic.
    /// Controls which slots are patient-self-schedulable vs staff-only.
    /// </summary>
    Task SetClinicSchedulingTiersAsync(string clinicId, ClinicSchedulingTierConfig tierConfig);

    /// <summary>
    /// Gets the scheduling tier configuration for this provider at a specific clinic.
    /// Returns null if no tier config is set.
    /// </summary>
    Task<ClinicSchedulingTierConfig?> GetClinicSchedulingTiersAsync(string clinicId);
}
