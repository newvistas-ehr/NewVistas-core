// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of prior-authorization requests for fast status queries.
/// Grain key: "PA-INDEX:{patientId}"
/// </summary>
public interface IPriorAuthIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(PriorAuthIndexEntry entry);

    Task RemoveAsync(string paId);

    Task<List<PriorAuthIndexEntry>> GetAllAsync();

    Task<List<PriorAuthIndexEntry>> GetPendingAsync();

    Task<List<PriorAuthIndexEntry>> GetApprovedAsync();

    Task ClearAsync();
}
