// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Immunization Forecasting — Site Flavor Architecture (Option 4: Composition).
/// Checks the IMMUNIZATION_FORECAST feature flag before delegating to the
/// optional IImmunizationForecastGrain. If the feature is not enabled,
/// the forecast grain is never activated and consumes no resources.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string ImmunizationForecastFeature = "IMMUNIZATION_FORECAST";

    public async Task<ImmunizationForecastResult> GenerateImmunizationForecastAsync()
    {
        // ── Feature gate (Site Flavor Architecture) ─────────────────
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ImmunizationForecastFeature);
        if (!enabled)
        {
            return new ImmunizationForecastResult
            {
                Success = false,
                ErrorMessage = "Immunization forecasting is not enabled for this site. Enable the IMMUNIZATION_FORECAST feature in Site Parameters."
            };
        }

        // ── Gather patient data ─────────────────────────────────────
        IPatientGrain patientGrain = GetPatientGrain();
        PatientState patient = await patientGrain.GetPatientAsync();

        if (patient.DateOfBirth == null)
        {
            return new ImmunizationForecastResult
            {
                Success = false,
                ErrorMessage = "Patient date of birth is required for immunization forecasting."
            };
        }

        List<ImmunizationEntry> immunizations = await patientGrain.GetImmunizationsAsync();

        // ── Delegate to the optional forecast grain ─────────────────
        string forecastKey = $"IMM-FORECAST:{PatientId}";
        IImmunizationForecastGrain forecastGrain =
            GrainFactory.GetGrain<IImmunizationForecastGrain>(forecastKey);

        return await forecastGrain.GenerateForecastAsync(
            patient.DateOfBirth.Value,
            immunizations);
    }

    public async Task<ImmunizationForecastResult> GetImmunizationForecastAsync()
    {
        // ── Feature gate ────────────────────────────────────────────
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ImmunizationForecastFeature);
        if (!enabled)
        {
            return new ImmunizationForecastResult
            {
                Success = false,
                ErrorMessage = "Immunization forecasting is not enabled for this site."
            };
        }

        string forecastKey = $"IMM-FORECAST:{PatientId}";
        IImmunizationForecastGrain forecastGrain =
            GrainFactory.GetGrain<IImmunizationForecastGrain>(forecastKey);

        ImmunizationForecastState state = await forecastGrain.GetForecastStateAsync();

        return new ImmunizationForecastResult
        {
            Success = true,
            Recommendations = state.Recommendations,
            ForecastDate = state.LastForecastDate ?? DateTime.MinValue,
            TotalDue = state.Recommendations.Count(r => r.Status == "DUE"),
            TotalOverdue = state.Recommendations.Count(r => r.Status == "OVERDUE"),
            TotalComplete = state.Recommendations.Count(r => r.Status == "COMPLETE")
        };
    }
}
