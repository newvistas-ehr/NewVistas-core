// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a single hospital bed.
/// Maps to VistA DG package — Bed Control / File #405.4.
/// Key: "BED:{facilityId}:{bedId}"
/// </summary>
[GenerateSerializer]
public class BedState
{
    /// <summary>Bed identifier (e.g., "MED-3A-101").</summary>
    [Id(0)] public string BedId { get; set; } = string.Empty;

    /// <summary>Ward/unit this bed belongs to.</summary>
    [Id(1)] public string WardId { get; set; } = string.Empty;

    /// <summary>Ward name.</summary>
    [Id(2)] public string WardName { get; set; } = string.Empty;

    /// <summary>Room number.</summary>
    [Id(3)] public string RoomNumber { get; set; } = string.Empty;

    /// <summary>Bed position in room (A, B, C, etc.).</summary>
    [Id(4)] public string BedPosition { get; set; } = string.Empty;

    /// <summary>Bed status: AVAILABLE, OCCUPIED, RESERVED, BLOCKED, CLEANING, OUT_OF_SERVICE.</summary>
    [Id(5)] public string Status { get; set; } = "AVAILABLE";

    /// <summary>Bed type: REGULAR, ICU, TELEMETRY, ISOLATION, OBSERVATION, PEDIATRIC.</summary>
    [Id(6)] public string BedType { get; set; } = "REGULAR";

    /// <summary>Currently assigned patient ID (null if empty).</summary>
    [Id(7)] public string? PatientId { get; set; }

    /// <summary>Currently assigned patient name.</summary>
    [Id(8)] public string? PatientName { get; set; }

    /// <summary>Admission date for current occupant.</summary>
    [Id(9)] public DateTime? OccupiedSince { get; set; }

    /// <summary>Expected discharge date for current occupant.</summary>
    [Id(10)] public DateTime? ExpectedDischargeDate { get; set; }

    /// <summary>Reserved for patient ID (for pending admissions).</summary>
    [Id(11)] public string? ReservedForPatientId { get; set; }

    /// <summary>Reserved for patient name.</summary>
    [Id(12)] public string? ReservedForPatientName { get; set; }

    /// <summary>Isolation type if applicable: CONTACT, DROPLET, AIRBORNE, PROTECTIVE.</summary>
    [Id(13)] public string? IsolationType { get; set; }

    /// <summary>Block reason if status is BLOCKED.</summary>
    [Id(14)] public string? BlockReason { get; set; }

    /// <summary>Facility ID.</summary>
    [Id(15)] public string FacilityId { get; set; } = string.Empty;

    /// <summary>Last modified.</summary>
    [Id(16)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight bed entry for board/census display.
/// </summary>
[GenerateSerializer]
public class BedSummaryEntry
{
    /// <summary>Bed ID.</summary>
    [Id(0)] public string BedId { get; set; } = string.Empty;

    /// <summary>Ward ID.</summary>
    [Id(1)] public string WardId { get; set; } = string.Empty;

    /// <summary>Ward name.</summary>
    [Id(2)] public string WardName { get; set; } = string.Empty;

    /// <summary>Room number.</summary>
    [Id(3)] public string RoomNumber { get; set; } = string.Empty;

    /// <summary>Bed position.</summary>
    [Id(4)] public string BedPosition { get; set; } = string.Empty;

    /// <summary>Status.</summary>
    [Id(5)] public string Status { get; set; } = string.Empty;

    /// <summary>Bed type.</summary>
    [Id(6)] public string BedType { get; set; } = string.Empty;

    /// <summary>Patient name if occupied.</summary>
    [Id(7)] public string? PatientName { get; set; }

    /// <summary>Isolation type.</summary>
    [Id(8)] public string? IsolationType { get; set; }
}

/// <summary>
/// State for the facility bed board — index of all beds.
/// Key: "BED-BOARD:{facilityId}"
/// </summary>
[GenerateSerializer]
public class BedBoardState
{
    /// <summary>All beds in the facility.</summary>
    [Id(0)] public List<BedSummaryEntry> Beds { get; set; } = new();
}
