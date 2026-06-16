// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// GPRA Report Grain — RPMS CIMGAGP / BQIGPRA.
/// Key: "GPRA-REPORT:{reportId}"
///
/// Aggregates CQM evaluations with fiscal year trending and baseline comparison.
/// Additive feature grain per Site Flavor Architecture (Option 4).
/// </summary>
public interface IGpraReportGrain : IGrainWithStringKey
{
    Task<GrainStates.GpraReportState> GetAsync();

    Task CreateAsync(
        int fiscalYear,
        GrainStates.GpraReportingPeriod reportingPeriod,
        DateTime currentPeriodStart, DateTime currentPeriodEnd,
        DateTime baselinePeriodStart, DateTime baselinePeriodEnd,
        string facilityId, string facilityName,
        string? communityTaxonomy,
        int activeUserPopulation,
        string? generatedById, string? generatedByName);

    /// <summary>Adds an evaluated indicator result to the report.</summary>
    Task AddIndicatorResultAsync(GrainStates.GpraIndicatorResult result);

    /// <summary>Marks the report as completed after all indicators are evaluated.</summary>
    Task CompleteAsync();

    /// <summary>Marks the report as errored.</summary>
    Task MarkErrorAsync(string errorMessage);

    /// <summary>Links a CQM report ID for drill-down.</summary>
    Task AddCqmReportLinkAsync(string cqmReportId);
}
