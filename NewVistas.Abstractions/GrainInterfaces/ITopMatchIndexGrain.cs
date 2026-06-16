// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of TOP offset matching records.
/// Grain key: "TOP-MATCH-IDX"
/// </summary>
public interface ITopMatchIndexGrain : IGrainWithStringKey
{
    Task<List<TopMatchIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(TopMatchIndexEntry entry);
    Task<List<TopMatchIndexEntry>> GetPendingAsync();
    Task<List<TopMatchIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
}
