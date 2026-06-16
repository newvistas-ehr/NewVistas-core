// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Crossmatch Index Grain — per-patient index of all crossmatch requests.
///
/// Grain key: "BB-XM-IDX:{patientId}"
/// </summary>
public interface ICrossmatchIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(CrossmatchIndexEntry entry);
    Task<List<CrossmatchIndexEntry>> GetAllAsync();
}
