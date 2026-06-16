// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-wide index grain for all outbreaks.
/// Key: "HAI-OUTBREAK-IDX"
/// </summary>
public interface IOutbreakIndexGrain : IGrainWithStringKey
{
    Task<List<OutbreakSummary>> GetAllOutbreaksAsync();

    Task<List<OutbreakSummary>> GetActiveAsync();

    Task UpsertOutbreakAsync(OutbreakSummary summary);

    Task RemoveOutbreakAsync(string outbreakId);
}
