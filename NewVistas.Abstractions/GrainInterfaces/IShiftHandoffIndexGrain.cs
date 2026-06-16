// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient shift handoff index.
/// Grain key: "NURS-HANDOFF-IDX:{patientId}"
/// </summary>
public interface IShiftHandoffIndexGrain : IGrainWithStringKey
{
    Task<List<ShiftHandoffIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(ShiftHandoffIndexEntry entry);
    Task<ShiftHandoffIndexEntry?> GetLatestAsync();
}
