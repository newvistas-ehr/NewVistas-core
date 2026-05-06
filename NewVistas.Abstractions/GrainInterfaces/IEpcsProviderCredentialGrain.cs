// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// EPCS Provider Credential Grain — 21 CFR Part 1311.105 / 1311.115.
/// Key: "EPCS-PROVIDER:{providerId}"
///
/// Manages provider identity proofing, 2FA configuration, and credential lifecycle.
/// </summary>
public interface IEpcsProviderCredentialGrain : IGrainWithStringKey
{
    Task<GrainStates.EpcsProviderCredentialState> GetAsync();

    Task SaveAsync(
        string providerId, string providerName,
        string? npi, string? deaNumber,
        GrainStates.IdentityProofingLevel identityProofingLevel,
        DateTime? identityProofingDate,
        List<GrainStates.EpcsTwoFactorMethod>? configuredTwoFactorMethods,
        string? certificateThumbprint, DateTime? certificateExpiration);

    Task ActivateAsync();
    Task SuspendAsync();
    Task RevokeAsync();
    Task RecordUsageAsync();
}
