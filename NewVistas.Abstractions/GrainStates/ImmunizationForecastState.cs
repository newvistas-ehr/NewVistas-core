// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the immunization forecast grain.
/// Stores ACIP vaccine schedule definitions and the most recent forecast result.
/// Maps to IHS RPMS Immunization Forecasting module.
/// </summary>
[GenerateSerializer]
public class ImmunizationForecastState
{
    /// <summary>
    /// Patient ID this forecast is for.
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// ACIP vaccine series definitions (the schedule to evaluate against).
    /// Seeded with standard ACIP schedule, customizable per site.
    /// </summary>
    [Id(1)]
    public List<VaccineSeriesDefinition> Schedule { get; set; } = new();

    /// <summary>
    /// Most recently generated forecast recommendations.
    /// </summary>
    [Id(2)]
    public List<ForecastRecommendation> Recommendations { get; set; } = new();

    /// <summary>
    /// When the forecast was last generated.
    /// </summary>
    [Id(3)]
    public DateTime? LastForecastDate { get; set; }

    [Id(4)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(5)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Defines a vaccine series from the ACIP schedule.
/// Maps to IHS RPMS BI TABLE VACCINE GROUP file.
/// </summary>
[GenerateSerializer]
public class VaccineSeriesDefinition
{
    /// <summary>
    /// Vaccine group name (e.g., "Hepatitis B", "DTaP", "COVID-19").
    /// </summary>
    [Id(0)]
    public string VaccineGroup { get; set; } = string.Empty;

    /// <summary>
    /// CVX codes that satisfy this series (e.g., ["08","44","110"] for Hep B).
    /// </summary>
    [Id(1)]
    public List<string> CvxCodes { get; set; } = new();

    /// <summary>
    /// Total number of doses required to complete the series.
    /// </summary>
    [Id(2)]
    public int DosesRequired { get; set; }

    /// <summary>
    /// Minimum age in months for the first dose (e.g., 0 for Hep B at birth).
    /// </summary>
    [Id(3)]
    public int MinAgeMonths { get; set; }

    /// <summary>
    /// Maximum age in months (0 = no upper limit).
    /// </summary>
    [Id(4)]
    public int MaxAgeMonths { get; set; }

    /// <summary>
    /// Minimum intervals in days between consecutive doses.
    /// Index 0 = interval between dose 1 and 2, index 1 = between dose 2 and 3, etc.
    /// </summary>
    [Id(5)]
    public List<int> MinIntervalDays { get; set; } = new();

    /// <summary>
    /// Recommended intervals in days between consecutive doses.
    /// </summary>
    [Id(6)]
    public List<int> RecommendedIntervalDays { get; set; } = new();

    /// <summary>
    /// Whether this is a recurring annual vaccine (e.g., Influenza).
    /// </summary>
    [Id(7)]
    public bool IsAnnual { get; set; }

    /// <summary>
    /// Display sort order.
    /// </summary>
    [Id(8)]
    public int SortOrder { get; set; }
}

/// <summary>
/// A single forecast recommendation for one vaccine series.
/// </summary>
[GenerateSerializer]
public class ForecastRecommendation
{
    /// <summary>
    /// Vaccine group this recommendation is for.
    /// </summary>
    [Id(0)]
    public string VaccineGroup { get; set; } = string.Empty;

    /// <summary>
    /// Forecast status: DUE, OVERDUE, UPCOMING, COMPLETE, CONTRAINDICATED, NOT_RECOMMENDED.
    /// </summary>
    [Id(1)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of doses already received in this series.
    /// </summary>
    [Id(2)]
    public int DosesReceived { get; set; }

    /// <summary>
    /// Total doses required for series completion.
    /// </summary>
    [Id(3)]
    public int DosesRequired { get; set; }

    /// <summary>
    /// Earliest valid date for the next dose (based on minimum intervals).
    /// </summary>
    [Id(4)]
    public DateTime? EarliestDate { get; set; }

    /// <summary>
    /// Recommended date for the next dose.
    /// </summary>
    [Id(5)]
    public DateTime? RecommendedDate { get; set; }

    /// <summary>
    /// Date of the most recent dose received in this series.
    /// </summary>
    [Id(6)]
    public DateTime? LastDoseDate { get; set; }

    /// <summary>
    /// Reason for the status (e.g., "Series complete", "2 months overdue").
    /// </summary>
    [Id(7)]
    public string? StatusReason { get; set; }
}

/// <summary>
/// Result returned from a forecast generation.
/// </summary>
[GenerateSerializer]
public class ImmunizationForecastResult
{
    [Id(0)]
    public bool Success { get; set; }

    [Id(1)]
    public List<ForecastRecommendation> Recommendations { get; set; } = new();

    [Id(2)]
    public DateTime ForecastDate { get; set; }

    [Id(3)]
    public string? ErrorMessage { get; set; }

    [Id(4)]
    public int TotalDue { get; set; }

    [Id(5)]
    public int TotalOverdue { get; set; }

    [Id(6)]
    public int TotalComplete { get; set; }
}
