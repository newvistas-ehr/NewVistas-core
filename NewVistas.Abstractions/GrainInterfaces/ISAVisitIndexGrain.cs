// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-episode SA visit index grain.
/// Key: "SA-VISIT-IDX:{episodeId}"
/// </summary>
public interface ISAVisitIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.SAVisitIndexEntry>> GetAllAsync();
    Task AddEntryAsync(GrainStates.SAVisitIndexEntry entry);
    Task<int> GetVisitCountAsync();
}
