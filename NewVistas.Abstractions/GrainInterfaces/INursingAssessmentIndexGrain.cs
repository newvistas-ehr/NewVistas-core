// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of nursing assessments.
/// Grain key: "NURS-ASSESS-IDX:{patientId}"
/// </summary>
public interface INursingAssessmentIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns the full index state (all assessment summaries, newest first).</summary>
    Task<NursingAssessmentIndexState> GetAsync();

    /// <summary>Adds or replaces a summary entry in the index.</summary>
    Task AddEntryAsync(NursingAssessmentIndexEntry entry);

    /// <summary>Updates the status of an existing index entry (e.g., Draft → Signed).</summary>
    Task UpdateEntryStatusAsync(string assessmentId, NursingAssessmentStatus status);
}
