// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    Task EnableFeatureAsync(string featureName);

    /// <summary>
    /// Disables a feature flag for this site.
    /// </summary>
    Task DisableFeatureAsync(string featureName);

    /// <summary>
    /// Checks if a specific feature is enabled for this site.
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string featureName);
}
