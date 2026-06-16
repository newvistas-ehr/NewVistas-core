// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-pregnancy prenatal visit index grain.
/// Key: "OB-VISIT-IDX:{pregnancyId}"
///
/// Maintains a lightweight list of visit summaries for a pregnancy,
/// allowing the flowsheet / visit history to render without activating
/// every individual visit grain.
/// </summary>
public interface IPrenatalVisitIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all visit index entries (newest first).</summary>
    Task<List<GrainStates.PrenatalVisitIndexEntry>> GetAllAsync();

    /// <summary>Adds a new visit summary entry.</summary>
    Task AddEntryAsync(GrainStates.PrenatalVisitIndexEntry entry);

    /// <summary>Returns visit count for the pregnancy.</summary>
    Task<int> GetVisitCountAsync();
}
