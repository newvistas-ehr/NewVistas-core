// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Nested Types ─────────────────────────────────────────────────────────────

/// <summary>
/// A single rendered section within a generated health summary report.
/// Each section corresponds to one enabled component from the template.
/// </summary>
[GenerateSerializer]
public record HealthSummarySectionResult
{
    /// <summary>The component type this section represents.</summary>
    [Id(0)] public HealthSummaryComponentType ComponentType { get; init; }

    /// <summary>Section header text (from config override or default).</summary>
    [Id(1)] public string SectionHeader { get; init; } = string.Empty;

    /// <summary>Formatted content lines for this section.</summary>
    [Id(2)] public List<string> ContentLines { get; init; } = new();

    /// <summary>Number of entries found for this section.</summary>
    [Id(3)] public int EntryCount { get; init; }
}

/// <summary>
/// Lightweight index entry for the per-patient summary history index.
/// </summary>
[GenerateSerializer]
public record HealthSummaryIndexEntry
{
    /// <summary>Unique report identifier.</summary>
    [Id(0)] public string ReportId { get; init; } = string.Empty;

    /// <summary>Patient this summary was generated for.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>ID of the template used to generate this summary.</summary>
    [Id(2)] public string TypeId { get; init; } = string.Empty;

    /// <summary>Name of the template used.</summary>
    [Id(3)] public string TypeName { get; init; } = string.Empty;

    /// <summary>When the summary was generated.</summary>
    [Id(4)] public DateTime GeneratedDate { get; init; }

    /// <summary>User ID who requested the summary.</summary>
    [Id(5)] public string GeneratedById { get; init; } = string.Empty;

    /// <summary>Display name of the requesting provider.</summary>
    [Id(6)] public string GeneratedByName { get; init; } = string.Empty;

    /// <summary>Number of sections included in the report.</summary>
    [Id(7)] public int SectionCount { get; init; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// State for a single generated health summary report instance.
/// Captures a point-in-time snapshot of a patient's clinical data
/// rendered from a health summary type template.
/// VistA HEALTH SUMMARY TYPE file (#142) / GMTS.m.
/// </summary>
[GenerateSerializer]
public class HealthSummaryState
{
    /// <summary>(.01) Unique identifier for this generated report.</summary>
    [Id(0)] public string ReportId { get; set; } = string.Empty;

    /// <summary>(.02) Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>(.03) ID of the template used to generate this report.</summary>
    [Id(2)] public string TypeId { get; set; } = string.Empty;

    /// <summary>(.04) Name of the template used.</summary>
    [Id(3)] public string TypeName { get; set; } = string.Empty;

    /// <summary>(.05) Timestamp when this report was generated.</summary>
    [Id(4)] public DateTime GeneratedDate { get; set; }

    /// <summary>(.06) User ID who requested generation.</summary>
    [Id(5)] public string GeneratedById { get; set; } = string.Empty;

    /// <summary>(.07) Display name of the requesting provider.</summary>
    [Id(6)] public string GeneratedByName { get; set; } = string.Empty;

    /// <summary>(.08) Rendered sections of the report, in display order.</summary>
    [Id(7)] public List<HealthSummarySectionResult> Sections { get; set; } = new();
}

// ─── Index State ──────────────────────────────────────────────────────────────

/// <summary>
/// State for the per-patient health summary history index grain.
/// </summary>
[GenerateSerializer]
public class HealthSummaryIndexState
{
    /// <summary>Patient identifier (same as the grain key suffix).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Index entries, newest first.</summary>
    [Id(1)] public List<HealthSummaryIndexEntry> Entries { get; set; } = new();
}
