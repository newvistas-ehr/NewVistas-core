// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility nursery census/board — one summary entry per newborn + level-of-care roll-up.
/// Singleton key: "NEONATE-NURSERY:DEFAULT".
/// </summary>
public interface INewbornNurseryGrain : IGrainWithStringKey
{
    Task UpsertEntryAsync(NewbornNurseryEntry entry);
    Task RemoveEntryAsync(string newbornId);
    Task<List<NewbornNurseryEntry>> GetAllAsync();
    Task<List<NewbornNurseryEntry>> GetActiveAsync();
    Task<List<NewbornNurseryEntry>> GetByLevelAsync(NurseryLevelOfCare level);
    Task<List<NewbornNurseryEntry>> GetWithPendingScreensAsync();
}
