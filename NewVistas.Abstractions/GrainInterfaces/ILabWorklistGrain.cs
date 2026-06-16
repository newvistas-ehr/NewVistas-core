// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab tech worklist grain — aggregated view of pending specimens, results, and verifications.
/// Grain key: "LAB-WORKLIST:{locationId}"
/// </summary>
public interface ILabWorklistGrain : IGrainWithStringKey
{
    Task<LabWorklistState> GetAsync();
    Task RefreshAsync(List<LabWorklistItem> items);
    Task AddItemAsync(LabWorklistItem item);
    Task RemoveItemAsync(string labTestId);
    Task<List<LabWorklistItem>> GetByCategoryAsync(LabWorklistCategory category);
    Task<List<LabWorklistItem>> GetCriticalAsync();
}
