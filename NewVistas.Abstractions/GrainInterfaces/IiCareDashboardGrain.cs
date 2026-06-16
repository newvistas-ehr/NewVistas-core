// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for iCare-style unified provider dashboard.
/// Enabled per site via ISiteParametersGrain.Features containing "ICARE_DASHBOARD".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to IHS RPMS iCare / BQI (Behavioral Health Quality Improvement) dashboard.
/// Provides a unified provider view combining clinical reminders, quality measure gaps,
/// and disease registry data for a patient panel.
///
/// Keyed by provider ID (e.g., "ICARE:{providerId}").
/// </summary>
public interface IiCareDashboardGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add a patient to this provider's panel.
    /// </summary>
    Task AddPatientToPanelAsync(string patientId, string patientName);

    /// <summary>
    /// Remove a patient from this provider's panel.
    /// </summary>
    Task RemovePatientFromPanelAsync(string patientId);

    /// <summary>
    /// Get the provider's panel patient list.
    /// </summary>
    Task<List<PanelPatient>> GetPanelAsync();

    /// <summary>
    /// Generate the iCare dashboard data for all patients on this provider's panel.
    /// Aggregates reminders, quality gaps, and registry data.
    /// </summary>
    Task<iCareDashboardResult> GenerateDashboardAsync();

    /// <summary>
    /// Get the most recently generated dashboard without recalculating.
    /// </summary>
    Task<iCareDashboardState> GetDashboardStateAsync();

    /// <summary>
    /// Generate dashboard data for a single patient (drill-down view).
    /// </summary>
    Task<iCarePatientSummary> GetPatientSummaryAsync(string patientId);
}
