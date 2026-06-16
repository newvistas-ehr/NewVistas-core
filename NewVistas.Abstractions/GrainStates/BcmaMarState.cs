// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single entry in the patient's Medication Administration Record (MAR).
/// One entry per inpatient order, updated each time a dose is administered.
/// Maps to VistA PSB (BCMA) package links to PSJ (Inpatient Pharmacy) File #55.
/// </summary>
[GenerateSerializer]
public class MarEntry
{
    /// <summary>Inpatient order ID from PSJ (#55)</summary>
    [Id(0)] public string OrderId { get; set; } = string.Empty;

    /// <summary>Drug name (.01)</summary>
    [Id(1)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Dose string, e.g. "25mg" (.08)</summary>
    [Id(2)] public string? Dosage { get; set; }

    /// <summary>Route of administration, e.g. "PO", "IV" (.07)</summary>
    [Id(3)] public string? Route { get; set; }

    /// <summary>Administration schedule, e.g. "Q6H", "BID" (.05)</summary>
    [Id(4)] public string? Schedule { get; set; }

    /// <summary>Order type: UNIT_DOSE, IV, or LVP</summary>
    [Id(5)] public string OrderType { get; set; } = string.Empty;

    /// <summary>Priority: ROUTINE, STAT, or ASAP</summary>
    [Id(6)] public string Priority { get; set; } = "ROUTINE";

    /// <summary>Ward location ID</summary>
    [Id(7)] public string WardId { get; set; } = string.Empty;

    /// <summary>Room and bed assignment, e.g. "301-A"</summary>
    [Id(8)] public string? RoomBed { get; set; }

    /// <summary>Ordering provider name</summary>
    [Id(9)] public string? ProviderName { get; set; }

    /// <summary>Order start date/time (.02)</summary>
    [Id(10)] public DateTime? StartDate { get; set; }

    /// <summary>Order stop date/time (.03)</summary>
    [Id(11)] public DateTime? StopDate { get; set; }

    /// <summary>Scheduled administration times derived from the order's schedule</summary>
    [Id(12)] public List<DateTime> ScheduledTimes { get; set; } = new();

    /// <summary>Date/time of the most recent administration event</summary>
    [Id(13)] public DateTime? LastAdminDateTime { get; set; }

    /// <summary>Status of last administration: GIVEN, NOT GIVEN, HELD, REFUSED</summary>
    [Id(14)] public string? LastAdminStatus { get; set; }

    /// <summary>Name of nurse who performed the last administration</summary>
    [Id(15)] public string? LastAdminBy { get; set; }

    /// <summary>Cumulative count of administration events recorded for this order</summary>
    [Id(16)] public int TotalAdministrations { get; set; }

    /// <summary>Ordered list of BCMA administration grain IDs linked to this order</summary>
    [Id(17)] public List<string> BcmaAdministrationIds { get; set; } = new();

    /// <summary>True while the underlying order is ACTIVE; false after discontinue/expire</summary>
    [Id(18)] public bool IsActive { get; set; } = true;
}

/// <summary>
/// Per-patient Medication Administration Record (MAR) state.
/// Grain key: "BCMA-MAR:{patientId}".
/// </summary>
[GenerateSerializer]
public class PatientMARState
{
    /// <summary>All MAR entries, one per associated inpatient order</summary>
    [Id(0)] public List<MarEntry> Entries { get; set; } = new();

    /// <summary>Last modification timestamp</summary>
    [Id(1)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
