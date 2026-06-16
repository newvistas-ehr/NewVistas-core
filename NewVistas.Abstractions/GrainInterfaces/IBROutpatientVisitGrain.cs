// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Outpatient Visit Grain — a single outpatient BR training session.
///
/// Derived from VistA Blind Rehabilitation module:
///   File #782.3 — BLIND REHABILITATION OUTPATIENT VISIT
///   Routine: ANRVOP.m
///
/// Grain key: "BR-VISIT:{visitId}"
/// </summary>
public interface IBROutpatientVisitGrain : IGrainWithStringKey
{
    /// <summary>Returns the full outpatient visit record.</summary>
    Task<BROutpatientVisitState> GetAsync();

    /// <summary>
    /// Creates an outpatient BR training visit record.
    /// Corresponds to VistA ANRVOP CREATE.
    /// </summary>
    Task CreateAsync(
        string visitId,
        string patientId,
        DateTime visitDate,
        BRTrainingArea trainingArea,
        string therapistId,
        string therapistName,
        string location,
        int durationMinutes,
        string? sessionNotes,
        List<string> skillsAddressed);

    /// <summary>Adds a progress note to an existing visit.</summary>
    Task AddProgressNoteAsync(string note, string authorId, string authorName);

    /// <summary>Marks the visit as completed with an outcome summary.</summary>
    Task CompleteAsync(string outcomeSummary, BRVisitOutcome outcome);

    /// <summary>Cancels a scheduled visit.</summary>
    Task CancelAsync(string reason);
}
