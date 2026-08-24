// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Security;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// System-level site parameters grain.
/// Based on VistA PARAMETER file (#8989.5) and PARAMETER DEFINITION (#8989.51).
/// Singleton grain keyed by facility/site ID (e.g., "SITE:DEFAULT").
/// Holds cross-cutting display and behavior settings (ORWCV parameters, etc.).
/// </summary>
public interface ISiteParametersGrain : IGrainWithStringKey
{
    Task<GrainStates.SiteParametersState> GetParametersAsync();

    /// <summary>
    /// Sets the number of recent vitals to cache on the patient grain.
    /// Maps to VistA ORWCV VITALS parameter.
    /// </summary>
    Task SetVitalsDisplayCountAsync(int count);

    /// <summary>
    /// Gets the number of recent vitals to cache on the patient grain.
    /// </summary>
    Task<int> GetVitalsDisplayCountAsync();

    /// <summary>
    /// Sets the number of recent orders to cache on the patient grain.
    /// Maps to VistA ORWCV ORDERS parameter.
    /// </summary>
    Task SetOrdersDisplayCountAsync(int count);

    /// <summary>
    /// Gets the number of recent orders to cache on the patient grain.
    /// </summary>
    Task<int> GetOrdersDisplayCountAsync();

    /// <summary>
    /// Sets the number of recent notes to cache on the patient grain.
    /// Maps to VistA ORWCV NOTES parameter.
    /// </summary>
    Task SetNotesDisplayCountAsync(int count);

    /// <summary>
    /// Gets the number of recent notes to cache on the patient grain.
    /// </summary>
    Task<int> GetNotesDisplayCountAsync();

    /// <summary>
    /// Sets the number of recent item IDs kept per clinical domain in
    /// PatientState (allergies are never capped).
    /// </summary>
    Task SetRecentItemsDisplayCountAsync(int count);

    /// <summary>
    /// Gets the number of recent item IDs kept per clinical domain in
    /// PatientState. Older IDs live in the per-domain history indexes.
    /// Default 5.
    /// </summary>
    Task<int> GetRecentItemsDisplayCountAsync();

    /// <summary>
    /// Sets a named parameter value.
    /// </summary>
    Task SetParameterAsync(string parameterName, string value);

    /// <summary>
    /// Gets a named parameter value, or null if not set.
    /// </summary>
    Task<string?> GetParameterAsync(string parameterName);

    // ── Site Feature Flags (Composition Pattern) ──────────────────────────

    /// <summary>
    /// Gets the set of enabled feature flags for this site.
    /// Used by the Site Flavor Architecture (Option 4 — Composition) to
    /// enable optional grains per site (e.g., "PATIENT_MERGE", "IMMUNIZATION_FORECAST").
    /// </summary>
    Task<HashSet<string>> GetFeaturesAsync();

    /// <summary>
    /// Enables a feature flag for this site.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The feature is in <see cref="GrainStates.SiteFeatures.OneWayDisable"/> and has already been
    /// permanently disabled here. Such a feature can never be re-enabled — see
    /// <see cref="GrainStates.SiteParametersState.PermanentlyDisabledFeatures"/> for why.
    /// </exception>
    Task EnableFeatureAsync(string featureName);

    /// <summary>
    /// Disables a feature flag for this site. If the feature is in
    /// <see cref="GrainStates.SiteFeatures.OneWayDisable"/> this is <b>irreversible</b> —
    /// which is why the operation requires the system-manager key: before the one-way latch
    /// existed this was a reversible toggle any authenticated caller could flip; now it can
    /// permanently end a site's data collection, so it is gated like the destructive
    /// administrative act it is.
    /// </summary>
    /// <remarks>
    /// Both overloads carry identical attributes deliberately: the call filters cache
    /// attributes by (interface, method NAME), so overloads sharing a name must never
    /// diverge in their security or audit declarations.
    /// </remarks>
    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    [AuditAction("SITE", "DISABLE_FEATURE", EntityType = "SITE_PARAMETERS")]
    Task DisableFeatureAsync(string featureName);

    /// <summary>
    /// Disables a feature flag, recording who did it and why. For a one-way feature this is
    /// <b>irreversible</b> and the attribution is kept permanently in the disable log.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    [AuditAction("SITE", "DISABLE_FEATURE", EntityType = "SITE_PARAMETERS")]
    Task DisableFeatureAsync(string featureName, string? byUserId, string? byUserName, string? reason);

    /// <summary>
    /// Checks if a specific feature is enabled for this site.
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string featureName);

    /// <summary>
    /// True when the feature was permanently disabled here and can never be turned back on.
    /// UI should render the toggle as dead-and-explained rather than merely off.
    /// </summary>
    Task<bool> IsFeaturePermanentlyDisabledAsync(string featureName);

    /// <summary>
    /// The audit trail of permanent (irreversible) feature disables at this site.
    /// </summary>
    Task<List<GrainStates.PermanentFeatureDisable>> GetPermanentDisableLogAsync();
}
