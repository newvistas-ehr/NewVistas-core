// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton EPCS provider credential index grain.
/// Key: "EPCS-PROVIDER-IDX"
/// </summary>
public interface IEpcsProviderIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.EpcsProviderIndexEntry>> GetAllAsync();
    Task<List<GrainStates.EpcsProviderIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.EpcsProviderIndexEntry entry);
}
