// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Transfusion Index Grain — per-patient index of all transfusion records.
///
/// Grain key: "BB-TX-IDX:{patientId}"
/// </summary>
public interface ITransfusionIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(TransfusionIndexEntry entry);
    Task<List<TransfusionIndexEntry>> GetAllAsync();
}
