// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the MPI Correlation grain — represents a patient's national identity
/// and all local facility correlations in the Master Patient Index.
///
/// Maps to VistA PATIENT CORRELATION file (#985):
///   .01  — ICN (Integration Control Number)
///   .02  — Patient Name
///   .03  — SSN
///   .04  — Date of Birth
///   .05  — Sex
///   1    — LOCAL CORRELATION multiple (facility + local DFN pairs)
///
/// The ICN format is 10 digits + "V" + 6-digit checksum (e.g., "1000000001V123456").
/// Assigned by the national MPI at Austin Information Technology Center (AITC).
/// </summary>
[GenerateSerializer]
public class MpiCorrelationState
{
    /// <summary>
    /// Integration Control Number — national unique patient identifier (.01).
    /// Format: 10 digits + "V" + 6-digit checksum.
    /// </summary>
    [Id(0)]
    public string Icn { get; set; } = string.Empty;

    /// <summary>
    /// Patient name in Last,First format (.02).
    /// </summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Social Security Number (.03).
    /// Stored as 9-digit string without dashes.
    /// </summary>
    [Id(2)]
    public string Ssn { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth (.04).
    /// </summary>
    [Id(3)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Sex (.05). "M" = Male, "F" = Female.
    /// </summary>
    [Id(4)]
    public string? Sex { get; set; }

    /// <summary>
    /// Local facility correlations — maps this ICN to local DFNs at each
    /// treating facility. Mirrors the LOCAL CORRELATION multiple (field 1)
    /// in File #985.
    /// </summary>
    [Id(5)]
    public List<MpiLocalCorrelation> LocalCorrelations { get; set; } = new();

    /// <summary>
    /// Whether this patient is deceased.
    /// </summary>
    [Id(6)]
    public bool IsDeceased { get; set; }

    /// <summary>
    /// Date of death, if deceased.
    /// </summary>
    [Id(7)]
    public DateTime? DateOfDeath { get; set; }

    /// <summary>
    /// When this correlation record was created.
    /// </summary>
    [Id(8)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this correlation record was last modified.
    /// </summary>
    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// If this ICN has been merged into another ICN (duplicate-patient merge,
    /// VistA DG MERGE / IHS Patient Merge equivalent), the surviving ICN.
    /// When set: this correlation record is an alias — clinicians and federation
    /// inbound should redirect lookups by this ICN to the surviving ICN.
    /// Once set, never cleared (merges are permanent; unmerge is a separate
    /// operation that issues a new ICN, not a re-activation of the old one).
    /// </summary>
    [Id(10)]
    public string? MergedIntoIcn { get; set; }
}

/// <summary>
/// A local facility correlation — maps the national ICN to a local patient
/// DFN (Data File Number) at a specific VistA facility.
///
/// Each entry in the LOCAL CORRELATION multiple (File #985, field 1) contains:
///   .01  — Facility (pointer to INSTITUTION file #4)
///   .02  — Local DFN (patient record number at that facility)
///   .03  — Correlation Date
///   .04  — Last Seen Date
/// </summary>
[GenerateSerializer]
public class MpiLocalCorrelation
{
    /// <summary>
    /// Facility identifier — corresponds to station number / INSTITUTION IEN (.01).
    /// </summary>
    [Id(0)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// Facility name for display purposes.
    /// </summary>
    [Id(1)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>
    /// Local DFN — the patient's record number at this facility (.02).
    /// </summary>
    [Id(2)]
    public string LocalDfn { get; set; } = string.Empty;

    /// <summary>
    /// Date the correlation was established (.03).
    /// </summary>
    [Id(3)]
    public DateTime CorrelationDate { get; set; }

    /// <summary>
    /// Date the patient was last seen at this facility (.04).
    /// </summary>
    [Id(4)]
    public DateTime? LastSeenDate { get; set; }
}
