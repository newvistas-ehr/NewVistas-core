// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A single home-care visit by one discipline.
/// Key pattern: "HHC-VISIT:{guid}". VistA File #750.1 (HOME HEALTH VISIT). HBVISIT.m
/// </summary>
public interface IHomeVisitGrain : IGrainWithStringKey
{
    /// <summary>Schedules a visit.</summary>
    Task ScheduleAsync(
        string episodeId,
        string patientId,
        string patientName,
        HomeCareDiscipline discipline,
        HomeVisitType visitType,
        DateTime scheduledDateTime,
        string clinicianId,
        string clinicianName,
        string reason);

    /// <summary>Marks the visit in-progress (check-in). EVV fields are reserved for Phase 2.</summary>
    Task StartAsync();

    /// <summary>Completes the visit with clinical content.</summary>
    Task CompleteAsync(
        int durationMinutes,
        string vitalSigns,
        List<string> interventions,
        string summary,
        string noteId,
        DateTime? nextVisitDate);

    /// <summary>Records a non-completion (Cancelled / NoAnswer / PatientRefused) with a reason.</summary>
    Task CancelAsync(HomeVisitStatus status, string reason);

    // ── Medicare (Phase 2): Electronic Visit Verification ──
    /// <summary>EVV check-in: records arrival time, location, and method; marks the visit in-progress.</summary>
    Task CheckInAsync(DateTime time, string location, EvvMethod method);

    /// <summary>EVV check-out: records departure time and location.</summary>
    Task CheckOutAsync(DateTime time, string location);

    Task<HomeVisitState> GetVisitAsync();
}
