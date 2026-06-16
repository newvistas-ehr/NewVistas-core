// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an Emergency Department visit/encounter.
/// Maps to VistA EDIS (Emergency Department Integration Software).
/// Tracks a patient from arrival through disposition in the ED.
/// </summary>
[GenerateSerializer]
public class EdVisitState
{
    /// <summary>Unique ED visit identifier.</summary>
    [Id(0)] public string VisitId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (cached for board display).</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Arrival date/time.</summary>
    [Id(3)] public DateTime ArrivalDateTime { get; set; }

    /// <summary>Mode of arrival: AMBULANCE, WALK_IN, HELICOPTER, POLICE, TRANSFER.</summary>
    [Id(4)] public string ArrivalMode { get; set; } = "WALK_IN";

    /// <summary>Chief complaint.</summary>
    [Id(5)] public string ChiefComplaint { get; set; } = string.Empty;

    /// <summary>Triage acuity level (ESI 1-5). 1=Resuscitation, 2=Emergent, 3=Urgent, 4=Less Urgent, 5=Non-Urgent.</summary>
    [Id(6)] public int AcuityLevel { get; set; }

    /// <summary>Triage date/time.</summary>
    [Id(7)] public DateTime? TriageDateTime { get; set; }

    /// <summary>Triage nurse ID.</summary>
    [Id(8)] public string? TriageNurseId { get; set; }

    /// <summary>Triage nurse name.</summary>
    [Id(9)] public string? TriageNurseName { get; set; }

    /// <summary>Assigned ED bed/room (e.g., "ED-BAY-3", "TRAUMA-1", "FAST-TRACK-2").</summary>
    [Id(10)] public string? AssignedBed { get; set; }

    /// <summary>Date/time bed was assigned.</summary>
    [Id(11)] public DateTime? BedAssignedDateTime { get; set; }

    /// <summary>Assigned attending physician ID.</summary>
    [Id(12)] public string? AttendingPhysicianId { get; set; }

    /// <summary>Assigned attending physician name.</summary>
    [Id(13)] public string? AttendingPhysicianName { get; set; }

    /// <summary>Assigned nurse ID.</summary>
    [Id(14)] public string? AssignedNurseId { get; set; }

    /// <summary>Assigned nurse name.</summary>
    [Id(15)] public string? AssignedNurseName { get; set; }

    /// <summary>Current status: WAITING, TRIAGED, IN_TREATMENT, AWAITING_RESULTS,
    /// AWAITING_ADMISSION, ADMITTED, DISCHARGED, LWBS (Left Without Being Seen),
    /// AMA (Against Medical Advice), TRANSFERRED, EXPIRED.</summary>
    [Id(16)] public string Status { get; set; } = "WAITING";

    /// <summary>Date/time physician first saw the patient.</summary>
    [Id(17)] public DateTime? PhysicianSeenDateTime { get; set; }

    /// <summary>Disposition: DISCHARGE, ADMIT, TRANSFER, AMA, LWBS, EXPIRED.</summary>
    [Id(18)] public string? Disposition { get; set; }

    /// <summary>Disposition date/time.</summary>
    [Id(19)] public DateTime? DispositionDateTime { get; set; }

    /// <summary>If admitted: destination ward/unit.</summary>
    [Id(20)] public string? AdmitDestination { get; set; }

    /// <summary>Discharge diagnosis (ICD-10 code or text).</summary>
    [Id(21)] public string? DischargeDiagnosis { get; set; }

    /// <summary>Ordered labs, radiology, or other pending results (comma-separated).</summary>
    [Id(22)] public string? PendingResults { get; set; }

    /// <summary>Patient-facing comments/notes visible on tracking board.</summary>
    [Id(23)] public string? BoardComment { get; set; }

    /// <summary>Created date.</summary>
    [Id(24)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modified date.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight entry for the ED tracking board.
/// </summary>
[GenerateSerializer]
public class EdBoardEntry
{
    /// <summary>Visit ID.</summary>
    [Id(0)] public string VisitId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Chief complaint.</summary>
    [Id(2)] public string ChiefComplaint { get; set; } = string.Empty;

    /// <summary>ESI acuity (1-5).</summary>
    [Id(3)] public int AcuityLevel { get; set; }

    /// <summary>Assigned bed.</summary>
    [Id(4)] public string? AssignedBed { get; set; }

    /// <summary>Attending physician name.</summary>
    [Id(5)] public string? AttendingPhysicianName { get; set; }

    /// <summary>Current status.</summary>
    [Id(6)] public string Status { get; set; } = string.Empty;

    /// <summary>Arrival time.</summary>
    [Id(7)] public DateTime ArrivalDateTime { get; set; }

    /// <summary>Wait time in minutes from arrival to now (or to seen).</summary>
    [Id(8)] public int WaitMinutes { get; set; }

    /// <summary>Board comment.</summary>
    [Id(9)] public string? BoardComment { get; set; }

    /// <summary>Arrival mode.</summary>
    [Id(10)] public string ArrivalMode { get; set; } = string.Empty;
}

/// <summary>
/// State for the ED tracking board index (per-facility).
/// </summary>
[GenerateSerializer]
public class EdBoardState
{
    /// <summary>All active ED visits (not yet disposed).</summary>
    [Id(0)] public List<EdBoardEntry> ActiveVisits { get; set; } = new();

    /// <summary>Total ED beds/bays available.</summary>
    [Id(1)] public int TotalBeds { get; set; } = 24;

    /// <summary>ED beds currently occupied.</summary>
    [Id(2)] public int OccupiedBeds { get; set; }
}
