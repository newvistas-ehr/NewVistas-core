// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for immunization forecasting (ACIP schedule evaluation).
/// Enabled per site via ISiteParametersGrain.Features containing "IMMUNIZATION_FORECAST".
/// Follows the Site Flavor Architecture (Option 4 — Composition): this grain
/// only activates when a site explicitly enables the IMMUNIZATION_FORECAST feature.
///
/// Maps to IHS RPMS Immunization Forecasting module (BI FORECAST RPCs).
/// Keyed by patient ID (e.g., "IMM-FORECAST:{patientId}").
/// </summary>
public interface IImmunizationForecastGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generate a forecast for the patient based on their immunization history,
    /// age, and the configured ACIP vaccine schedule.
    /// </summary>
    Task<ImmunizationForecastResult> GenerateForecastAsync(
        DateTime patientDateOfBirth,
        List<ImmunizationEntry> immunizationHistory);

    /// <summary>
    /// Get the most recently generated forecast without recalculating.
    /// </summary>
    Task<ImmunizationForecastState> GetForecastStateAsync();

    /// <summary>
    /// Get all configured vaccine series definitions (the ACIP schedule).
    /// </summary>
    Task<List<VaccineSeriesDefinition>> GetScheduleAsync();

    /// <summary>
    /// Add or update a vaccine series definition in the schedule.
    /// Allows sites to customize the ACIP schedule.
    /// </summary>
    Task AddOrUpdateSeriesDefinitionAsync(VaccineSeriesDefinition definition);

    /// <summary>
    /// Remove a vaccine series definition from the schedule.
    /// </summary>
    Task RemoveSeriesDefinitionAsync(string vaccineGroup);
}
