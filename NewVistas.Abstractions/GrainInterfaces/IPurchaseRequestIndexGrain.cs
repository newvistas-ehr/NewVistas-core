// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-control-point index of purchase requests (VA Form 2237).
/// Grain key: "IFCAP-PR-IDX:{controlPointId}"
/// </summary>
public interface IPurchaseRequestIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a purchase request entry in the index.</summary>
    Task AddOrUpdateAsync(PurchaseRequestIndexEntry entry);

    /// <summary>Returns all purchase requests for this control point.</summary>
    Task<List<PurchaseRequestIndexEntry>> GetAllAsync();

    /// <summary>
    /// Returns requests in Draft or Submitted status (awaiting action).
    /// </summary>
    Task<List<PurchaseRequestIndexEntry>> GetPendingAsync();
}
