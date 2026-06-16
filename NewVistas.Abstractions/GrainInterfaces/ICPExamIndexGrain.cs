// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of Compensation &amp; Pension examination records.
/// Grain key: "CP-EXAM-IDX:{patientId}"
/// </summary>
public interface ICPExamIndexGrain : IGrainWithStringKey
{
    /// <summary>Inserts or updates an exam summary entry in the index.</summary>
    Task UpsertExamAsync(GrainStates.CPExamIndexEntry entry);

    /// <summary>Returns all exam summaries for this patient, newest scheduled date first.</summary>
    Task<List<GrainStates.CPExamIndexEntry>> GetAllExamsAsync();

    /// <summary>Returns exams with Scheduled or Rescheduled status.</summary>
    Task<List<GrainStates.CPExamIndexEntry>> GetScheduledExamsAsync();

    /// <summary>Returns exams with Completed status.</summary>
    Task<List<GrainStates.CPExamIndexEntry>> GetCompletedExamsAsync();
}
