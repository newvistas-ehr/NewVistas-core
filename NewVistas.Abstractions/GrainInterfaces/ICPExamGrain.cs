// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single Compensation &amp; Pension examination record.
/// VistA File #396 (COMPENSATION AND PENSION EXAMINATION).
/// Grain key: "CP-EXAM:{guid}"
/// Maps to DVBAB5.m, DVBABEXT.m routines.
/// </summary>
public interface ICPExamGrain : IGrainWithStringKey
{
    /// <summary>
    /// Schedules a new C&amp;P exam for a patient linked to a VBA claim.
    /// Status → Scheduled.
    /// </summary>
    Task ScheduleExamAsync(
        string patientId,
        string patientName,
        GrainStates.CPExamType examType,
        DateTime scheduledDate,
        string examinerName,
        string examinerTitle,
        GrainStates.CPExaminerType examinerType,
        string examLocation,
        string examFacility,
        string claimNumber,
        string benefitType,
        List<string> disabilityClaimedCodes,
        string createdBy);

    /// <summary>
    /// Records exam completion with diagnoses and nexus opinion.
    /// Status → Completed.
    /// </summary>
    Task CompleteExamAsync(
        List<string> diagnoses,
        bool nexus,
        string nexusRationale);

    /// <summary>Cancels the exam with a stated reason. Status → Cancelled.</summary>
    Task CancelExamAsync(string cancellationReason);

    /// <summary>Reschedules the exam to a new date. Status → Rescheduled.</summary>
    Task RescheduleExamAsync(DateTime newScheduledDate, string reason);

    /// <summary>Associates a completed DBQ with this exam.</summary>
    Task AddDbqToExamAsync(string dbqId);

    /// <summary>Returns the full state of this exam record.</summary>
    Task<GrainStates.CPExamState> GetExamAsync();
}
