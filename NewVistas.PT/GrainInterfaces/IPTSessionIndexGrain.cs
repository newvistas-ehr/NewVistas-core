// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Per-patient, per-body-group index of PT session grain keys.
/// Maintains entries sorted by SessionDate descending (most recent first).
/// Key format: "PTINDEX:{patientId}:{bodyGroup}"
/// </summary>
public interface IPTSessionIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a session key to the index, maintaining descending date order.</summary>
    Task AddSessionKeyAsync(string sessionGrainKey, DateTime sessionDate, BodyGroup bodyGroup, Laterality side);

    /// <summary>Removes a session key from the index.</summary>
    Task RemoveSessionKeyAsync(string sessionGrainKey);

    /// <summary>Returns all index entries sorted by date descending.</summary>
    Task<List<PTSessionIndexEntry>> GetAllSessionsAsync();

    /// <summary>Returns the most recent N sessions (for the "last 2" comparison view).</summary>
    Task<List<PTSessionIndexEntry>> GetLastNSessionsAsync(int count);

    /// <summary>Returns sessions within a date range.</summary>
    Task<List<PTSessionIndexEntry>> GetSessionsByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>Returns the total number of sessions recorded.</summary>
    Task<int> GetCountAsync();
}
