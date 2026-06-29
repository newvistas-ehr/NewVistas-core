// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Type of home visit. VistA File #750.1 (.04).</summary>
public enum HomeVisitType
{
    /// <summary>The initial / start-of-care visit.</summary>
    Initial,
    Routine,
    Urgent,
    Supervisory,
    Discharge,
    PhoneContact,
    /// <summary>Reserved (Phase 2): Medicare resumption-of-care visit after an inpatient stay.</summary>
    Resumption,
    /// <summary>Reserved (Phase 2): Medicare recertification visit.</summary>
    Recertification
}

/// <summary>Status of a home visit. VistA File #750.1 (.05).</summary>
public enum HomeVisitStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    NoAnswer,
    PatientRefused
}

/// <summary>
/// Reserved (Phase 2): Electronic Visit Verification method (Medicaid-mandated under the
/// 21st Century Cures Act). Dormant in Phase 1 (HBPC visits are not EVV-verified).
/// </summary>
public enum EvvMethod
{
    None,
    Gps,
    Telephony,
    Fob,
    Manual
}

/// <summary>
/// A single home-care visit by one discipline. Carries the EVV and Medicare visit-type fields
/// from the start so the Phase-2 Medicare extension needs no reshaping.
/// Key pattern: "HHC-VISIT:{guid}". VistA File #750.1 (HOME HEALTH VISIT). HBVISIT.m
/// </summary>
[GenerateSerializer]
public class HomeVisitState
{
    /// <summary>Unique visit identifier (grain key).</summary>
    [Id(0)] public string VisitId { get; set; } = string.Empty;

    /// <summary>Owning episode (HHC-EPISODE id).</summary>
    [Id(1)] public string EpisodeId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA File #750.1 (.01).</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (denormalized for the visit roster).</summary>
    [Id(3)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Scheduled (or actual) visit date/time. VistA File #750.1 (.02).</summary>
    [Id(4)] public DateTime ScheduledDateTime { get; set; }

    /// <summary>Discipline performing the visit. VistA File #750.1 (.03).</summary>
    [Id(5)] public HomeCareDiscipline Discipline { get; set; }

    /// <summary>Type of visit. VistA File #750.1 (.04).</summary>
    [Id(6)] public HomeVisitType VisitType { get; set; }

    /// <summary>Current visit status. VistA File #750.1 (.05).</summary>
    [Id(7)] public HomeVisitStatus Status { get; set; } = HomeVisitStatus.Scheduled;

    /// <summary>Clinician performing the visit.</summary>
    [Id(8)] public string ClinicianId { get; set; } = string.Empty;
    [Id(9)] public string ClinicianName { get; set; } = string.Empty;

    /// <summary>Duration in minutes (set on completion).</summary>
    [Id(10)] public int DurationMinutes { get; set; }

    /// <summary>Brief vitals notation (e.g. "BP 138/84, HR 72, O2 96%").</summary>
    [Id(11)] public string VitalSigns { get; set; } = string.Empty;

    /// <summary>Clinical interventions performed during the visit.</summary>
    [Id(12)] public List<string> Interventions { get; set; } = new();

    /// <summary>Short visit summary / patient response &amp; progress.</summary>
    [Id(13)] public string Summary { get; set; } = string.Empty;

    /// <summary>Optional link to a full signed TIU note for the visit.</summary>
    [Id(14)] public string NoteId { get; set; } = string.Empty;

    /// <summary>Reason for the visit (chief focus).</summary>
    [Id(15)] public string Reason { get; set; } = string.Empty;

    /// <summary>Date of the next planned visit (entered at completion).</summary>
    [Id(16)] public DateTime? NextVisitDate { get; set; }

    /// <summary>Reason for cancellation / non-completion.</summary>
    [Id(17)] public string CancellationReason { get; set; } = string.Empty;

    [Id(18)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(19)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── Reserved (Phase 2 / EVV) ─────────────────────────────────────────────
    /// <summary>Reserved (Phase 2): EVV check-in time.</summary>
    [Id(20)] public DateTime? CheckInTime { get; set; }
    /// <summary>Reserved (Phase 2): EVV check-out time.</summary>
    [Id(21)] public DateTime? CheckOutTime { get; set; }
    /// <summary>Reserved (Phase 2): EVV check-in location (lat,long or address).</summary>
    [Id(22)] public string CheckInLocation { get; set; } = string.Empty;
    /// <summary>Reserved (Phase 2): EVV check-out location.</summary>
    [Id(23)] public string CheckOutLocation { get; set; } = string.Empty;
    /// <summary>Reserved (Phase 2): EVV verification method.</summary>
    [Id(24)] public EvvMethod EvvMethod { get; set; } = EvvMethod.None;
}

/// <summary>Summary entry for home-visit index queries (per episode / per clinician).</summary>
[GenerateSerializer]
public class HomeVisitIndexEntry
{
    [Id(0)] public string VisitId { get; set; } = string.Empty;
    [Id(1)] public string EpisodeId { get; set; } = string.Empty;
    [Id(2)] public string PatientId { get; set; } = string.Empty;
    [Id(3)] public string PatientName { get; set; } = string.Empty;
    [Id(4)] public DateTime ScheduledDateTime { get; set; }
    [Id(5)] public HomeCareDiscipline Discipline { get; set; }
    [Id(6)] public HomeVisitType VisitType { get; set; }
    [Id(7)] public HomeVisitStatus Status { get; set; }
    [Id(8)] public string ClinicianId { get; set; } = string.Empty;
    [Id(9)] public string ClinicianName { get; set; } = string.Empty;
    [Id(10)] public int DurationMinutes { get; set; }
}

/// <summary>Persistent state for the facility-wide home-visit index grain.</summary>
[GenerateSerializer]
public class HomeVisitIndexState
{
    [Id(0)] public List<HomeVisitIndexEntry> Visits { get; set; } = new();
    [Id(1)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
