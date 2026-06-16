// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a single ward's patient census.
/// Derived view — not a direct VistA file, but mirrors the WARD CENSUS display in CPRS.
/// </summary>
[GenerateSerializer]
public class WardCensusState
{
    /// <summary>Ward identifier, e.g. "WARD-ICU-1".</summary>
    [Id(0)] public string WardId { get; set; } = string.Empty;

    /// <summary>Ward display name.</summary>
    [Id(1)] public string WardName { get; set; } = string.Empty;

    /// <summary>Currently admitted patients on this ward.</summary>
    [Id(2)] public List<WardCensusEntry> Patients { get; set; } = new();

    /// <summary>Timestamp of last census change.</summary>
    [Id(3)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A single patient's current census entry on a ward.
/// Mirrors the WARD CENSUS board view used by nursing and pharmacy.
/// </summary>
[GenerateSerializer]
public class WardCensusEntry
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient display name (cached from PatientGrain for fast census queries).</summary>
    [Id(1)] public string? PatientName { get; set; }

    /// <summary>Most recent ADT movement ID for this episode.</summary>
    [Id(2)] public string AdmissionId { get; set; } = string.Empty;

    /// <summary>Original admission date/time for this episode.</summary>
    [Id(3)] public DateTime AdmitDate { get; set; }

    /// <summary>Current room-bed assignment, e.g. "301-A".</summary>
    [Id(4)] public string? RoomBed { get; set; }

    /// <summary>Current treating specialty.</summary>
    [Id(5)] public string? TreatingSpecialty { get; set; }

    /// <summary>Current attending physician name.</summary>
    [Id(6)] public string? AttendingPhysicianName { get; set; }

    /// <summary>DateTime of the most recent movement affecting this census entry.</summary>
    [Id(7)] public DateTime LastMovementDate { get; set; }
}
