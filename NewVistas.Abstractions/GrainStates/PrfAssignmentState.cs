// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single PRF flag assignment for a patient (VistA Files #26.11 PRF LOCAL FLAG
/// and #26.13 PRF ASSIGNMENT). Managed by DGPF* MUMPS routines.
/// </summary>
[GenerateSerializer]
public record PrfFlagAssignment
{
    /// <summary>Identifier of the flag definition (links to PRF flag record).</summary>
    [Id(0)] public string FlagId { get; init; } = string.Empty;

    /// <summary>Name of the flag (e.g., BEHAVIORAL, HIGH RISK FOR SUICIDE).</summary>
    [Id(1)] public string FlagName { get; init; } = string.Empty;

    /// <summary>Flag type: NATIONAL or LOCAL.</summary>
    [Id(2)] public string FlagType { get; init; } = string.Empty;

    /// <summary>Whether this is a VistA national flag (vs. site-local).</summary>
    [Id(3)] public bool IsNational { get; init; }

    /// <summary>Date the flag was assigned to the patient.</summary>
    [Id(4)] public DateTime AssignedDate { get; init; }

    /// <summary>User ID who assigned the flag.</summary>
    [Id(5)] public string AssignedByUserId { get; init; } = string.Empty;

    /// <summary>Display name of the user who assigned the flag.</summary>
    [Id(6)] public string AssignedByUserName { get; init; } = string.Empty;

    /// <summary>Date of the most recent flag review.</summary>
    [Id(7)] public DateTime? ReviewDate { get; init; }

    /// <summary>User ID who performed the most recent review.</summary>
    [Id(8)] public string? ReviewedByUserId { get; init; }

    /// <summary>Display name of the reviewer.</summary>
    [Id(9)] public string? ReviewedByUserName { get; init; }

    /// <summary>Clinical narrative explaining why the flag was assigned or reviewed.</summary>
    [Id(10)] public string? Narrative { get; init; }

    /// <summary>Whether this flag assignment is currently active.</summary>
    [Id(11)] public bool IsActive { get; init; } = true;

    /// <summary>Date the flag was deactivated, if applicable.</summary>
    [Id(12)] public DateTime? DeactivatedDate { get; init; }

    /// <summary>Reason the flag was deactivated.</summary>
    [Id(13)] public string? DeactivatedReason { get; init; }
}

/// <summary>
/// Aggregate of all PRF flag assignments for a single patient (VistA Files #26.11, #26.13).
/// </summary>
[GenerateSerializer]
public class PrfAssignmentState
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All flag assignments (active and historical).</summary>
    [Id(1)] public List<PrfFlagAssignment> Assignments { get; set; } = new();

    /// <summary>Date of the most recent review of any flag for this patient.</summary>
    [Id(2)] public DateTime? LastReviewDate { get; set; }

    /// <summary>Date by which the next flag review is due.</summary>
    [Id(3)] public DateTime? ReviewDueDate { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(4)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(5)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
