// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for fee basis authorizations.
/// Grain key: "FEE-AUTH-IDX:{patientId}".
/// </summary>
public interface IFeeAuthorizationIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new authorization entry or updates an existing one (matched by AuthorizationId).</summary>
    Task AddOrUpdateAsync(FeeAuthorizationIndexEntry entry);

    /// <summary>Returns all authorization entries for this patient.</summary>
    Task<List<FeeAuthorizationIndexEntry>> GetAllAsync();

    /// <summary>Returns only Active or Pending authorization entries.</summary>
    Task<List<FeeAuthorizationIndexEntry>> GetActiveAsync();
}
