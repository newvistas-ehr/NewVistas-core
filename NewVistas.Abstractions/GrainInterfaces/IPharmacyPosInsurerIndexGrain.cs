// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton POS insurer index grain.
/// Key: "POS-INSURER-IDX"
/// </summary>
public interface IPharmacyPosInsurerIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.PosInsurerIndexEntry>> GetAllAsync();
    Task<List<GrainStates.PosInsurerIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.PosInsurerIndexEntry entry);
}
