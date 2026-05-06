// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for a payer's EDI eligibility verification configuration.
/// Stores connection details, supported service types, and routing information.
/// Grain key: "PAYER-CFG:{payerId}"
/// </summary>
public interface IPayerConfigGrain : IGrainWithStringKey
{
    /// <summary>Returns the current payer configuration state.</summary>
    Task<PayerConfigState> GetAsync();

    /// <summary>
    /// Creates or updates the payer configuration.
    /// </summary>
    Task CreateAsync(
        string payerName,
        bool supportsRealTimeEligibility,
        string? interchangeReceiverId,
        string? interchangeReceiverQualifier,
        string? eligibilityEndpoint,
        string? eligibilityPhone,
        List<string>? supportedServiceTypeCodes,
        string? notes);

    /// <summary>Deactivates this payer configuration.</summary>
    Task DeactivateAsync();

    /// <summary>Reactivates a previously deactivated payer configuration.</summary>
    Task ActivateAsync();
}
