// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a patient merge job. Persists the audit trail of what was merged.
/// Maps to VistA DG MERGE file (#15.1).
/// </summary>
[GenerateSerializer]
public class PatientMergeState
{
    /// <summary>
    /// Merge job ID (grain key).
    /// </summary>
    [Id(0)]
    public string MergeId { get; set; } = string.Empty;

    /// <summary>
    /// The surviving patient ID (target — keeps their demographics).
    /// </summary>
    [Id(1)]
    public string TargetPatientId { get; set; } = string.Empty;

    /// <summary>
    /// The duplicate patient ID (source — deactivated after merge).
    /// </summary>
    [Id(2)]
    public string SourcePatientId { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the merge.
    /// </summary>
    [Id(3)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// User who authorized/executed the merge.
    /// </summary>
    [Id(4)]
    public string MergedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the merge author.
    /// </summary>
    [Id(5)]
    public string MergedByUserName { get; set; } = string.Empty;

    /// <summary>
    /// When the merge was executed.
    /// </summary>
    [Id(6)]
    public DateTime MergeDate { get; set; }

    /// <summary>
    /// Current status: PENDING, COMPLETED, FAILED.
    /// </summary>
    [Id(7)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Error message if the merge failed.
    /// </summary>
    [Id(8)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Counts of items moved from source to target, by category.
    /// </summary>
    [Id(9)]
    public Dictionary<string, int> ItemsMoved { get; set; } = new();
}

/// <summary>
/// Result returned from a patient merge operation.
/// </summary>
[GenerateSerializer]
public class PatientMergeResult
{
    [Id(0)]
    public bool Success { get; set; }

    [Id(1)]
    public string MergeId { get; set; } = string.Empty;

    [Id(2)]
    public string? ErrorMessage { get; set; }

    [Id(3)]
    public Dictionary<string, int> ItemsMoved { get; set; } = new();
}
