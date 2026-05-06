// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Individual prior-authorization request lifecycle grain.
/// Grain key: "PA:{guid}"
/// </summary>
public interface IPriorAuthorizationGrain : IGrainWithStringKey
{
    Task<PriorAuthorizationState> GetAsync();

    Task SubmitRequestAsync(string patientId, string? drugId, string drugName,
        string? planId, string? providerId, string? providerName,
        List<string> diagnosisCodes, string? clinicalJustification);

    Task ApproveAsync(string reviewerId, string reviewerName,
        string? notes, DateTime? expirationDate);

    Task DenyAsync(string reviewerId, string reviewerName, string reason);

    Task ExpireAsync();

    Task CancelAsync();
}
