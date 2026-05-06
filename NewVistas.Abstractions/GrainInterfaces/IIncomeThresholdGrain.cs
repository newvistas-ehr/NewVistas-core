// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain holding VA income threshold reference data for means-test
/// determinations (VistA File #408.15 INCOME THRESHOLDS).
/// Grain key: "INCOME-THRESHOLD-IDX".
/// </summary>
public interface IIncomeThresholdGrain : IGrainWithStringKey
{
    /// <summary>Returns the full income threshold state (all entries, all years).</summary>
    Task<IncomeThresholdState> GetAsync();

    /// <summary>Returns all threshold entries for the specified fiscal year.</summary>
    Task<List<IncomeThresholdEntry>> GetByYearAsync(int year);

    /// <summary>
    /// Seeds representative FY 2024 VA income threshold values for all four categories
    /// across eight household sizes. Idempotent — no-op if already seeded for that year.
    /// </summary>
    Task SeedDefaultsAsync(int year);

    /// <summary>
    /// Looks up the threshold amount for a specific year, category, and household size.
    /// Returns null if no matching entry exists.
    /// </summary>
    Task<decimal?> LookupThresholdAsync(int year, string category, int householdSize);

    /// <summary>Manually sets or updates a single threshold value.</summary>
    Task SetThresholdAsync(int year, string category, int householdSize, decimal amount);
}
