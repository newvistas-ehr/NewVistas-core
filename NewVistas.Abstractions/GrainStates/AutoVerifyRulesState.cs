// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Auto-Verify Rules grain. Defines per-instrument rules
/// that determine whether lab results can be automatically verified
/// (released) without manual review.
///
/// Mirrors VistA's auto-release functionality configured in AUTO INSTRUMENT
/// file (#62.4, field 99) and the result validation logic in LA7VIN7.m.
/// </summary>
[GenerateSerializer]
public class AutoVerifyRulesState
{
    /// <summary>
    /// Instrument ID these rules apply to
    /// </summary>
    [Id(0)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>
    /// Whether auto-verification is globally enabled for this instrument
    /// </summary>
    [Id(1)]
    public bool Enabled { get; set; }

    /// <summary>
    /// Per-test verification rules
    /// </summary>
    [Id(2)]
    public List<TestVerifyRule> TestRules { get; set; } = new();

    /// <summary>
    /// Maximum message age (minutes) for auto-verification.
    /// Results older than this require manual review.
    /// </summary>
    [Id(3)]
    public int MaxMessageAgeMinutes { get; set; } = 60;

    /// <summary>
    /// Whether specimen barcode verification is required
    /// </summary>
    [Id(4)]
    public bool RequireSpecimenBarcode { get; set; }

    /// <summary>
    /// Whether duplicate result confirmation is required
    /// (two consecutive identical results from different runs)
    /// </summary>
    [Id(5)]
    public bool RequireDuplicateConfirmation { get; set; }

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(6)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Auto-verify rules for a specific test on a specific instrument.
/// Mirrors VistA's reportable test configuration in AUTO INSTRUMENT subfile.
/// </summary>
[GenerateSerializer]
public class TestVerifyRule
{
    /// <summary>
    /// LOINC code this rule applies to
    /// </summary>
    [Id(0)]
    public string LoincCode { get; set; } = string.Empty;

    /// <summary>
    /// Test name for display
    /// </summary>
    [Id(1)]
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Normal range low — results below this trigger manual review
    /// </summary>
    [Id(2)]
    public double? NormalLow { get; set; }

    /// <summary>
    /// Normal range high — results above this trigger manual review
    /// </summary>
    [Id(3)]
    public double? NormalHigh { get; set; }

    /// <summary>
    /// Critical low — results below this trigger critical alert + manual review
    /// </summary>
    [Id(4)]
    public double? CriticalLow { get; set; }

    /// <summary>
    /// Critical high — results above this trigger critical alert + manual review
    /// </summary>
    [Id(5)]
    public double? CriticalHigh { get; set; }

    /// <summary>
    /// Delta check percentage — if result differs from previous by more than
    /// this percentage, flag for manual review
    /// </summary>
    [Id(6)]
    public double? DeltaCheckPercent { get; set; }

    /// <summary>
    /// Delta check absolute value — if result differs from previous by more
    /// than this absolute amount, flag for manual review
    /// </summary>
    [Id(7)]
    public double? DeltaCheckAbsolute { get; set; }

    /// <summary>
    /// Whether this specific test can be auto-verified
    /// </summary>
    [Id(8)]
    public bool AutoVerifyEnabled { get; set; } = true;
}

/// <summary>
/// Result of auto-verification evaluation.
/// </summary>
[GenerateSerializer]
public enum AutoVerifyDecision
{
    /// <summary>Result within normal range — auto-verify</summary>
    ACCEPT,
    /// <summary>Result outside normal range or delta check failed — requires manual review</summary>
    REVIEW,
    /// <summary>Result in critical range — critical alert + manual review required</summary>
    CRITICAL_ALERT
}
