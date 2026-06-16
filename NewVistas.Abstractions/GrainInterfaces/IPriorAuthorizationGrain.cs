// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
