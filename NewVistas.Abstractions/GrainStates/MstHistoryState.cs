// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── MST screening status values ─────────────────────────────────────────────

/// <summary>Outcome of an MST screening interview (VistA File #29.11).</summary>
[GenerateSerializer]
public enum MstStatus
{
    /// <summary>Veteran was not yet asked about MST.</summary>
    Unasked = 0,

    /// <summary>Veteran experienced MST (positive screen).</summary>
    Verified = 1,

    /// <summary>Veteran denied MST experience.</summary>
    Denied = 2,

    /// <summary>Veteran declined to answer.</summary>
    NoResponse = 3,
}

// ─── MST screening history entry ─────────────────────────────────────────────

/// <summary>
/// A single MST (Military Sexual Trauma) screening encounter (VistA File #29.11).
/// </summary>
[GenerateSerializer]
public record MstScreeningEntry
{
    /// <summary>Unique identifier for this screening record.</summary>
    [Id(0)] public string ScreeningId { get; init; } = string.Empty;

    /// <summary>Date the screening was conducted.</summary>
    [Id(1)] public DateTime ScreeningDate { get; init; }

    /// <summary>Outcome of the screening interview.</summary>
    [Id(2)] public MstStatus ScreeningStatus { get; init; }

    /// <summary>User ID who conducted the screening.</summary>
    [Id(3)] public string ScreenedByUserId { get; init; } = string.Empty;

    /// <summary>Display name of the clinician who performed the screening.</summary>
    [Id(4)] public string ScreenedByUserName { get; init; } = string.Empty;

    /// <summary>Clinical location where the screening occurred.</summary>
    [Id(5)] public string? Location { get; init; }

    /// <summary>Additional notes about this screening encounter.</summary>
    [Id(6)] public string? Notes { get; init; }
}

// ─── MST History aggregate — VistA File #29.11 MST HISTORY ──────────────────

/// <summary>
/// Aggregate MST history for a single patient (VistA File #29.11 MST HISTORY).
/// Managed by DGMST* MUMPS routines.
/// </summary>
[GenerateSerializer]
public class MstHistoryState
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Current overall MST status based on most recent positive screen.</summary>
    [Id(1)] public MstStatus CurrentStatus { get; set; } = MstStatus.Unasked;

    /// <summary>Date of the most recent MST screening.</summary>
    [Id(2)] public DateTime? LastScreeningDate { get; set; }

    /// <summary>Whether any screening has resulted in a positive MST determination.</summary>
    [Id(3)] public bool MstPositive { get; set; }

    /// <summary>Clinical location where the veteran disclosed MST experience.</summary>
    [Id(4)] public string? DisclosureLocation { get; set; }

    /// <summary>Date of the original MST disclosure.</summary>
    [Id(5)] public DateTime? DisclosureDate { get; set; }

    /// <summary>Full chronological history of all MST screenings.</summary>
    [Id(6)] public List<MstScreeningEntry> Screenings { get; set; } = new();

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(7)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(8)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
