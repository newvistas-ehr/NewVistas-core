// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Bed lifecycle — VistA DGPM Bed Control (File #405.4) with an explicit EVS
/// (Environmental Services) turnover cycle. "Placeable" is DERIVED, never stored:
/// only <see cref="Available"/> counts — a dirty bed is not a free bed.
/// </summary>
[GenerateSerializer]
public enum BedLifecycleState
{
    /// <summary>Clean and placeable.</summary>
    Available = 0,
    /// <summary>Held for a pending admission or incoming transfer.</summary>
    Reserved = 1,
    /// <summary>A patient is in the bed.</summary>
    Occupied = 2,
    /// <summary>Vacated, awaiting EVS turnover — NOT placeable.</summary>
    Dirty = 3,
    /// <summary>EVS turnover in progress.</summary>
    Cleaning = 4,
    /// <summary>Administrative hold (reason required) — staffing, isolation buffer, etc.</summary>
    Blocked = 5,
    /// <summary>Maintenance / decommissioned — excluded from operational capacity.</summary>
    OutOfService = 6
}

/// <summary>Bed type — File #405.4 room-bed description / specialty bed classes.</summary>
[GenerateSerializer]
public enum BedType
{
    Regular = 0,
    Icu = 1,
    Telemetry = 2,
    Isolation = 3,
    Observation = 4,
    Pediatric = 5,
    Bassinet = 6
}

/// <summary>Infection-control isolation precautions for a bed or room.</summary>
[GenerateSerializer]
public enum BedIsolationType
{
    None = 0,
    Contact = 1,
    Droplet = 2,
    Airborne = 3,
    Protective = 4
}

/// <summary>Gender policy for shared rooms.</summary>
[GenerateSerializer]
public enum RoomGenderPolicy
{
    Any = 0,
    MaleOnly = 1,
    FemaleOnly = 2
}

/// <summary>
/// A physical room in a unit. OPTIONAL — small sites leave <see cref="InpatientUnitState.Rooms"/>
/// empty and beds carry RoomId = "" (UI renders bed-only).
/// </summary>
[GenerateSerializer]
public class InpatientRoom
{
    /// <summary>Room identifier unique within the unit (e.g. "301").</summary>
    [Id(0)] public string RoomId { get; set; } = string.Empty;

    /// <summary>Optional display name.</summary>
    [Id(1)] public string? Name { get; set; }

    /// <summary>Gender policy for the room (shared-room constraint).</summary>
    [Id(2)] public RoomGenderPolicy GenderPolicy { get; set; }

    /// <summary>Room-level isolation — applies to every bed in the room.</summary>
    [Id(3)] public BedIsolationType RoomIsolation { get; set; }

    /// <summary>False = room closed (beds inside should be Blocked/OutOfService).</summary>
    [Id(4)] public bool IsActive { get; set; } = true;
}

/// <summary>
/// One bed — VistA ROOM-BED (File #405.4) merged with the NURSING UNIT bed
/// assignment (File #210). The unit grain is the single writer; the bed's
/// occupant, reservation, EVS, and nursing fields all live here and nowhere else.
/// </summary>
[GenerateSerializer]
public class InpatientBed
{
    /// <summary>Bed identifier unique within the unit (e.g. "301-A" or "1").</summary>
    [Id(0)] public string BedId { get; set; } = string.Empty;

    /// <summary>Owning room, or "" when the unit doesn't model rooms.</summary>
    [Id(1)] public string RoomId { get; set; } = string.Empty;

    [Id(2)] public BedType BedType { get; set; }

    [Id(3)] public BedLifecycleState State { get; set; }

    // ── Occupant ────────────────────────────────────────────────────────
    [Id(4)] public string? PatientId { get; set; }
    [Id(5)] public string? PatientName { get; set; }

    /// <summary>ADT movement (File #405) that placed the occupant.</summary>
    [Id(6)] public string? MovementId { get; set; }
    [Id(7)] public DateTime? OccupiedSince { get; set; }
    [Id(8)] public DateTime? ExpectedDischargeDate { get; set; }
    [Id(9)] public string? TreatingSpecialty { get; set; }
    [Id(10)] public string? AttendingPhysicianId { get; set; }
    [Id(11)] public string? AttendingPhysicianName { get; set; }

    // ── Reservation (expiry enforced by a lazy sweep in the unit grain) ──
    [Id(12)] public string? ReservedForPatientId { get; set; }
    [Id(13)] public string? ReservedForPatientName { get; set; }
    [Id(14)] public DateTime? ReservationExpiresAt { get; set; }

    // ── Isolation / blocks ──────────────────────────────────────────────
    [Id(15)] public BedIsolationType Isolation { get; set; }

    /// <summary>Reason for Blocked or OutOfService state.</summary>
    [Id(16)] public string? BlockReason { get; set; }

    // ── EVS turnover ────────────────────────────────────────────────────
    [Id(17)] public DateTime? DirtySince { get; set; }
    [Id(18)] public DateTime? CleaningStartedAt { get; set; }
    [Id(19)] public string? CleaningByUserName { get; set; }
    [Id(20)] public DateTime? LastCleanedAt { get; set; }

    // ── Nursing (folded from NURSING UNIT File #210 bed assignment) ─────
    [Id(21)] public string? AttendingNurseId { get; set; }
    [Id(22)] public string? AttendingNurseName { get; set; }
    [Id(23)] public AcuityLevel? AcuityLevel { get; set; }

    [Id(24)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A patient on the unit census WITHOUT a bed — ED boarding, or a small site
/// that admits to the unit but doesn't track individual beds. Keeps the census
/// honest either way.
/// </summary>
[GenerateSerializer]
public class UnitBoarder
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>ADT movement (File #405) that admitted the patient to the unit.</summary>
    [Id(2)] public string MovementId { get; set; } = string.Empty;
    [Id(3)] public DateTime AdmitDate { get; set; }
    [Id(4)] public string? TreatingSpecialty { get; set; }
    [Id(5)] public string? AttendingPhysicianName { get; set; }
}

/// <summary>
/// Inpatient unit — merges VistA WARD LOCATION (File #42) and NURSING UNIT (File #210).
/// The SINGLE writer/owner of its rooms, beds, occupancy, reservations, EVS turnover,
/// and nursing bed assignments. Grain key: "UNIT:{institutionId}:{unitId}".
/// Census and capacity are projections of this state — never stored elsewhere.
/// </summary>
[GenerateSerializer]
public class InpatientUnitState
{
    /// <summary>Unit identifier, unique within the institution (e.g. "MED-3A").</summary>
    [Id(0)] public string UnitId { get; set; } = string.Empty;

    /// <summary>Owning institution (File #4).</summary>
    [Id(1)] public string InstitutionId { get; set; } = string.Empty;

    [Id(2)] public string Name { get; set; } = string.Empty;

    /// <summary>Clinical specialty of the unit: MedSurg, ICU, PCU, PACU, Psych, OB, Oncology, LTC...</summary>
    [Id(3)] public string? UnitType { get; set; }

    /// <summary>Default treating specialty stamped on admissions when the caller gives none.</summary>
    [Id(4)] public string? DefaultTreatingSpecialty { get; set; }

    [Id(5)] public bool IsActive { get; set; } = true;

    /// <summary>OPTIONAL room modeling — may stay empty (small sites render bed-only).</summary>
    [Id(6)] public List<InpatientRoom> Rooms { get; set; } = new();

    [Id(7)] public List<InpatientBed> Beds { get; set; } = new();

    /// <summary>Patients admitted to the unit without a bed (ED boarding / bedless sites).</summary>
    [Id(8)] public List<UnitBoarder> Boarders { get; set; } = new();

    /// <summary>Charge nurse for the current shift (File #210).</summary>
    [Id(9)] public string? ChargeNurseId { get; set; }
    [Id(10)] public string? ChargeNurseName { get; set; }

    [Id(11)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>The one honest definition of "placeable now".</summary>
    public static bool IsPlaceable(BedLifecycleState s) => s == BedLifecycleState.Available;
}

/// <summary>
/// Census row — a PROJECTION of unit state (replaces the old WardCensusEntry).
/// Computed on read, never stored separately, so it can't drift from bed truth.
/// </summary>
[GenerateSerializer]
public class UnitCensusEntry
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>null = boarder (admitted to the unit, no bed).</summary>
    [Id(2)] public string? BedId { get; set; }
    [Id(3)] public string? RoomId { get; set; }
    [Id(4)] public string MovementId { get; set; } = string.Empty;
    [Id(5)] public DateTime AdmitDate { get; set; }
    [Id(6)] public string? TreatingSpecialty { get; set; }
    [Id(7)] public string? AttendingPhysicianName { get; set; }
    [Id(8)] public string? AttendingNurseName { get; set; }
    [Id(9)] public AcuityLevel? AcuityLevel { get; set; }
}

/// <summary>
/// Arguments for placing a patient on a unit. The ADT movement id is generated by
/// the workflow BEFORE this call so the unit can record it (idempotency key: retrying
/// the same MovementId is a no-op success).
/// </summary>
[GenerateSerializer]
public class UnitAdmissionRequest
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>ADT movement id (File #405), generated by the workflow before the call.</summary>
    [Id(2)] public string MovementId { get; set; } = string.Empty;

    /// <summary>null → the patient is carried as a unit boarder (no bed).</summary>
    [Id(3)] public string? BedId { get; set; }
    [Id(4)] public DateTime AdmitDate { get; set; }
    [Id(5)] public DateTime? ExpectedDischargeDate { get; set; }
    [Id(6)] public string? TreatingSpecialty { get; set; }
    [Id(7)] public string? AttendingPhysicianId { get; set; }
    [Id(8)] public string? AttendingPhysicianName { get; set; }

    /// <summary>Optional; when set, establishes the nurse's UnitCoverage treatment relationship (ADR-002).</summary>
    [Id(9)] public string? AttendingNurseId { get; set; }
    [Id(10)] public string? AttendingNurseName { get; set; }

    /// <summary>Occupy a bed reserved for a DIFFERENT patient (bed-control override).</summary>
    [Id(11)] public bool OverrideReservation { get; set; }
}
