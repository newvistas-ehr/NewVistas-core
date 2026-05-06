// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Income Threshold Entry ────────────────────────────────────────────────────

/// <summary>
/// A single VA income threshold value for a given fiscal year, geographic/category
/// tier, and household size (VistA File #408.15 INCOME THRESHOLDS).
/// </summary>
[GenerateSerializer]
public record IncomeThresholdEntry
{
    /// <summary>
    /// Composite key: "{year}-{category}-{householdSize}" (e.g., "2024-GMT_LOW-1").
    /// Used for idempotent seeding and O(1) lookup.
    /// </summary>
    [Id(0)] public string EntryId { get; init; } = string.Empty;

    /// <summary>VA fiscal year to which this threshold applies (e.g., 2024).</summary>
    [Id(1)] public int FiscalYear { get; init; }

    /// <summary>
    /// Threshold category:
    /// "GMT_LOW" — Geographic Means Test lower tier;
    /// "GMT_MED" — Geographic Means Test middle tier;
    /// "GMT_HIGH" — Geographic Means Test upper tier;
    /// "HEC_COPAY" — Health Eligibility Center copayment threshold.
    /// </summary>
    [Id(2)] public string Category { get; init; } = string.Empty;

    /// <summary>Number of persons in the veteran's household (1–8; 8 = 8 or more).</summary>
    [Id(3)] public int HouseholdSize { get; init; }

    /// <summary>Annual income threshold amount in US dollars.</summary>
    [Id(4)] public decimal ThresholdAmount { get; init; }
}

// ─── Income Threshold State ────────────────────────────────────────────────────

/// <summary>
/// Singleton grain holding VA income threshold reference data across all fiscal
/// years and geographic tiers (VistA File #408.15).
/// Grain key: "INCOME-THRESHOLD-IDX".
/// </summary>
[GenerateSerializer]
public class IncomeThresholdState
{
    /// <summary>All threshold entries across all seeded fiscal years.</summary>
    [Id(0)] public List<IncomeThresholdEntry> Entries { get; set; } = new();

    /// <summary>Most recent fiscal year for which defaults have been seeded (0 if never seeded).</summary>
    [Id(1)] public int LastSeededYear { get; set; }

    /// <summary>UTC timestamp of the most recent update (seed or manual).</summary>
    [Id(2)] public DateTime? LastUpdatedDate { get; set; }
}
