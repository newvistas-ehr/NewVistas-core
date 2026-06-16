// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// GPRA reporting period type — RPMS CIMGAGP.m.
/// </summary>
[GenerateSerializer]
public enum GpraReportingPeriod
{
    FullFiscalYear = 0,
    Quarter1 = 1,
    Quarter2 = 2,
    Quarter3 = 3,
    Quarter4 = 4,
}

/// <summary>
/// GPRA report status.
/// </summary>
[GenerateSerializer]
public enum GpraReportStatus
{
    Draft = 0,
    Evaluating = 1,
    Completed = 2,
    Error = 3,
}

/// <summary>
/// GPRA clinical category — RPMS BQIGPRA.m category field.
/// </summary>
[GenerateSerializer]
public enum GpraClinicalCategory
{
    Diabetes = 0,
    CardiovascularDisease = 1,
    WomensHealth = 2,
    Immunizations = 3,
    BehavioralHealth = 4,
    PreventiveCare = 5,
    Asthma = 6,
    ChildHealth = 7,
    OralHealth = 8,
    ObstetricsGynecology = 9,
}

// ── Nested Types ─────────────────────────────────────────────────────────────

/// <summary>
/// Result for a single GPRA indicator within a report.
/// Wraps the CQM evaluation with baseline comparison.
/// </summary>
[GenerateSerializer]
public class GpraIndicatorResult
{
    /// <summary>CQM measure ID this indicator evaluates (e.g., "GPRA-DM-01").</summary>
    [Id(0)]
    public string MeasureId { get; set; } = string.Empty;

    /// <summary>Indicator title (e.g., "Diabetes: HbA1c Testing").</summary>
    [Id(1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Clinical category.</summary>
    [Id(2)]
    public GpraClinicalCategory Category { get; set; }

    // ── Current Period ───────────────────────────────────────────────

    /// <summary>Denominator count for current period.</summary>
    [Id(3)]
    public int CurrentDenominator { get; set; }

    /// <summary>Numerator count for current period.</summary>
    [Id(4)]
    public int CurrentNumerator { get; set; }

    /// <summary>Performance rate for current period (0-100%).</summary>
    [Id(5)]
    public decimal CurrentPerformanceRate { get; set; }

    // ── Baseline Period ──────────────────────────────────────────────

    /// <summary>Denominator count for baseline period.</summary>
    [Id(6)]
    public int BaselineDenominator { get; set; }

    /// <summary>Numerator count for baseline period.</summary>
    [Id(7)]
    public int BaselineNumerator { get; set; }

    /// <summary>Performance rate for baseline period (0-100%).</summary>
    [Id(8)]
    public decimal BaselinePerformanceRate { get; set; }

    // ── Comparison ───────────────────────────────────────────────────

    /// <summary>Percentage point change (current - baseline).</summary>
    [Id(9)]
    public decimal PercentagePointChange { get; set; }

    /// <summary>Whether performance improved from baseline.</summary>
    [Id(10)]
    public bool IsImproved { get; set; }

    /// <summary>GPRA target rate (if defined), e.g., 50.0 for 50%.</summary>
    [Id(11)]
    public decimal? TargetRate { get; set; }

    /// <summary>Whether the target was met.</summary>
    [Id(12)]
    public bool TargetMet { get; set; }
}

// ── Main State Classes ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for a GPRA Report grain (GPRA-REPORT:{reportId}).
/// Aggregates multiple CQM measure evaluations with fiscal year trending
/// and baseline comparison — maps to RPMS CIMGAGP and BQIGPRA.
/// </summary>
[GenerateSerializer]
public class GpraReportState
{
    /// <summary>Unique grain key (GPRA-REPORT:{guid}).</summary>
    [Id(0)]
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Report status.</summary>
    [Id(1)]
    public GpraReportStatus Status { get; set; }

    /// <summary>Fiscal year (e.g., 2026).</summary>
    [Id(2)]
    public int FiscalYear { get; set; }

    /// <summary>Reporting period within the fiscal year.</summary>
    [Id(3)]
    public GpraReportingPeriod ReportingPeriod { get; set; }

    /// <summary>Current period start date.</summary>
    [Id(4)]
    public DateTime CurrentPeriodStart { get; set; }

    /// <summary>Current period end date.</summary>
    [Id(5)]
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>Baseline period start date (typically 3 years prior).</summary>
    [Id(6)]
    public DateTime BaselinePeriodStart { get; set; }

    /// <summary>Baseline period end date.</summary>
    [Id(7)]
    public DateTime BaselinePeriodEnd { get; set; }

    /// <summary>Facility/site identifier for this report.</summary>
    [Id(8)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>Facility name.</summary>
    [Id(9)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>Community taxonomy (RPMS AUTTAX) — optional.</summary>
    [Id(10)]
    public string? CommunityTaxonomy { get; set; }

    /// <summary>Total active user population for this facility/community.</summary>
    [Id(11)]
    public int ActiveUserPopulation { get; set; }

    /// <summary>Individual indicator results.</summary>
    [Id(12)]
    public List<GpraIndicatorResult> Indicators { get; set; } = new();

    /// <summary>IDs of CQM reports generated during evaluation (for drill-down).</summary>
    [Id(13)]
    public List<string> CqmReportIds { get; set; } = new();

    /// <summary>Error message if evaluation failed.</summary>
    [Id(14)]
    public string? ErrorMessage { get; set; }

    /// <summary>User who generated the report.</summary>
    [Id(15)]
    public string? GeneratedById { get; set; }

    /// <summary>User name who generated the report.</summary>
    [Id(16)]
    public string? GeneratedByName { get; set; }

    [Id(17)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(18)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index Entries ────────────────────────────────────────────────────────────

[GenerateSerializer]
public class GpraReportIndexEntry
{
    [Id(0)] public string ReportId { get; set; } = string.Empty;
    [Id(1)] public int FiscalYear { get; set; }
    [Id(2)] public GpraReportingPeriod ReportingPeriod { get; set; }
    [Id(3)] public GpraReportStatus Status { get; set; }
    [Id(4)] public string FacilityName { get; set; } = string.Empty;
    [Id(5)] public int ActiveUserPopulation { get; set; }
    [Id(6)] public int IndicatorCount { get; set; }
    [Id(7)] public DateTime CreatedDate { get; set; }
}

[GenerateSerializer]
public class GpraReportIndexState
{
    [Id(0)] public List<GpraReportIndexEntry> Entries { get; set; } = new();
}
