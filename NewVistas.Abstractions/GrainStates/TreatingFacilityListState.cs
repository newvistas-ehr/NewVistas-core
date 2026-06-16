// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single treating facility relationship for a patient (VistA File #391.91
/// TREATING FACILITY LIST). Managed by VAFHLTR.m and VAEFSPID.m routines.
/// </summary>
[GenerateSerializer]
public record TreatingFacilityEntry
{
    /// <summary>Station number or VISN identifier for the treating facility.</summary>
    [Id(0)] public string FacilityId { get; init; } = string.Empty;

    /// <summary>Display name of the treating facility.</summary>
    [Id(1)] public string FacilityName { get; init; } = string.Empty;

    /// <summary>Facility type (e.g., VAMC, CBOC, COMMUNITY CARE, DOD).</summary>
    [Id(2)] public string? FacilityType { get; init; }

    /// <summary>Date of the patient's most recent activity at this facility.</summary>
    [Id(3)] public DateTime? LastActivityDate { get; init; }

    /// <summary>Whether the treating relationship is currently active.</summary>
    [Id(4)] public bool IsActive { get; init; } = true;

    /// <summary>Type of care relationship (e.g., INPATIENT, OUTPATIENT, BOTH).</summary>
    [Id(5)] public string? RelationshipType { get; init; }
}

/// <summary>
/// Aggregate list of all treating facilities for a single patient
/// (VistA File #391.91 TREATING FACILITY LIST).
/// </summary>
[GenerateSerializer]
public class TreatingFacilityListState
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All treating facility relationships (active and historical).</summary>
    [Id(1)] public List<TreatingFacilityEntry> Facilities { get; set; } = new();

    /// <summary>Identifier of the patient's primary (enrollment) facility.</summary>
    [Id(2)] public string? PrimaryFacilityId { get; set; }

    /// <summary>Name of the patient's primary (enrollment) facility.</summary>
    [Id(3)] public string? PrimaryFacilityName { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(4)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(5)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
