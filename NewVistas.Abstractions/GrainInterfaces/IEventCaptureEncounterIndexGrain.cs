// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Event Capture Encounter Index Grain — application-wide searchable index of
/// Event Capture encounter summaries for cross-patient workload queries.
/// Enables workload reporting by provider, DSS unit, date range, and location.
/// MUMPS routines: ECPEWL.m (workload list), ECPEWLR.m (workload report).
///
/// Grain key: "EC-ENCOUNTER-IDX" (singleton)
/// </summary>
public interface IEventCaptureEncounterIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds or updates an encounter summary in the index.
    /// Called by the workflow grain when an encounter is created or completed.
    /// </summary>
    Task AddOrUpdateAsync(GrainStates.EventCaptureIndexEntry entry);

    /// <summary>
    /// Searches encounters with optional filters.
    /// Returns results sorted by encounter date descending.
    /// </summary>
    Task<List<GrainStates.EventCaptureIndexEntry>> SearchAsync(
        string? patientId,
        string? dssUnitId,
        string? providerId,
        GrainStates.EcEncounterStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        int maxResults);

    /// <summary>Returns all encounter summaries for a specific patient (for reporting).</summary>
    Task<List<GrainStates.EventCaptureIndexEntry>> GetByPatientAsync(string patientId, int maxResults);

    /// <summary>Returns all encounter summaries for a specific DSS unit.</summary>
    Task<List<GrainStates.EventCaptureIndexEntry>> GetByDssUnitAsync(string dssUnitId, int maxResults);
}
