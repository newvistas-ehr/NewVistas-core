// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of Disability Benefits Questionnaire (DBQ) records.
/// Grain key: "CP-DBQ-IDX:{patientId}"
/// </summary>
public interface IDBQIndexGrain : IGrainWithStringKey
{
    /// <summary>Inserts or updates a DBQ summary entry in the index.</summary>
    Task UpsertDBQAsync(GrainStates.DBQIndexEntry entry);

    /// <summary>Returns all DBQ summaries for this patient, newest first.</summary>
    Task<List<GrainStates.DBQIndexEntry>> GetAllDBQsAsync();

    /// <summary>Returns only DBQs linked to a specific exam.</summary>
    Task<List<GrainStates.DBQIndexEntry>> GetDBQsForExamAsync(string examId);

    /// <summary>Returns DBQs with Completed or Signed status.</summary>
    Task<List<GrainStates.DBQIndexEntry>> GetCompletedDBQsAsync();
}
