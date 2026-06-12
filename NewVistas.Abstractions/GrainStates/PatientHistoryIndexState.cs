// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single item reference in a per-patient, per-domain history index.
/// </summary>
[GenerateSerializer]
public class HistoryRef
{
    /// <summary>
    /// Grain key of the referenced clinical item (e.g., a consult ID).
    /// </summary>
    [Id(0)]
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// Clinical date of the item, used for newest-first paging. Null for
    /// entries migrated from legacy PatientState ID lists (their dates were
    /// not cheaply recoverable at migration time); null-dated entries sort
    /// after dated ones, preserving insertion order among themselves.
    /// </summary>
    [Id(1)]
    public DateTime? Date { get; set; }
}

/// <summary>
/// Persistent state for IPatientHistoryIndexGrain — the complete, append-only
/// list of item IDs for one clinical domain of one patient.
///
/// PatientState keeps only the most recent N IDs per domain (site parameter
/// RecentItemsDisplayCount); this grain holds the full history and is
/// activated only when a user actively requests items beyond the recent window.
/// </summary>
[GenerateSerializer]
public class PatientHistoryIndexState
{
    /// <summary>
    /// All item references for this patient+domain, in append (chronological)
    /// order. Deduplicated by ItemId on write.
    /// </summary>
    [Id(0)]
    public List<HistoryRef> Entries { get; set; } = new();
}
