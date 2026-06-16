// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the PatientLabSummary grain.
/// Holds the most recent result per test type for fast "current labs" queries.
/// </summary>
[GenerateSerializer]
public class PatientLabSummaryState
{
    /// <summary>
    /// Key: LOINC code (e.g., "2160-0" for Creatinine).
    /// Value: Most recent result plus short trending history.
    /// </summary>
    [Id(0)]
    public Dictionary<string, LabTestSummaryEntry> CurrentResults { get; set; } = new();

    /// <summary>
    /// Last time this summary was updated. Used for staleness checks.
    /// </summary>
    [Id(1)]
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// Summary entry for a single lab test type within the PatientLabSummary grain.
/// Contains the most recent value and a short trend (last 3).
/// </summary>
[GenerateSerializer]
public class LabTestSummaryEntry
{
    [Id(0)]
    public string LoincCode { get; set; } = string.Empty;

    [Id(1)]
    public string TestName { get; set; } = string.Empty;

    [Id(2)]
    public string Value { get; set; } = string.Empty;

    [Id(3)]
    public string Units { get; set; } = string.Empty;

    [Id(4)]
    public string ReferenceRange { get; set; } = string.Empty;

    [Id(5)]
    public LabAbnormalFlag AbnormalFlag { get; set; }

    [Id(6)]
    public DateTimeOffset ResultDate { get; set; }

    [Id(7)]
    public string FacilityCode { get; set; } = string.Empty;

    /// <summary>
    /// Last 3 values for this test type, most recent first.
    /// Enables trend display without activating any other grain.
    /// </summary>
    [Id(8)]
    public List<LabTrendPoint> RecentTrend { get; set; } = new();
}

/// <summary>
/// A single trend data point for lab result trending.
/// </summary>
[GenerateSerializer]
public class LabTrendPoint
{
    [Id(0)]
    public string Value { get; set; } = string.Empty;

    [Id(1)]
    public DateTimeOffset ResultDate { get; set; }

    [Id(2)]
    public LabAbnormalFlag AbnormalFlag { get; set; }
}
