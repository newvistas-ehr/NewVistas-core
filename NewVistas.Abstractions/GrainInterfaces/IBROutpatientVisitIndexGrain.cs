// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Outpatient Visit Index Grain — per-patient index of outpatient BR visits.
///
/// Grain key: "BR-VISIT-IDX:{patientId}"
/// </summary>
public interface IBROutpatientVisitIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all outpatient visit index entries for the patient.</summary>
    Task<List<BROutpatientVisitIndexEntry>> GetAllAsync();

    /// <summary>Returns visits within a date range.</summary>
    Task<List<BROutpatientVisitIndexEntry>> GetByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>Adds a new visit entry to the index.</summary>
    Task AddAsync(BROutpatientVisitIndexEntry entry);

    /// <summary>Updates the status of an existing visit entry.</summary>
    Task UpdateStatusAsync(string visitId, BRVisitStatus status);
}
