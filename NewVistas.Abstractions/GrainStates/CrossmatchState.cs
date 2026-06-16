// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Clinical urgency for a crossmatch request.</summary>
[GenerateSerializer]
public enum CrossmatchUrgency
{
    Routine = 0,
    Urgent = 1,
    Stat = 2,
    Emergent = 3
}

/// <summary>Compatibility test result (VistA BB CROSSMATCH file #65.03).</summary>
[GenerateSerializer]
public enum CrossmatchResult
{
    Pending = 0,
    Compatible = 1,
    Incompatible = 2,
    NotRequired = 3,
    Cancelled = 4
}

/// <summary>Laboratory method used to perform the crossmatch.</summary>
[GenerateSerializer]
public enum CrossmatchMethod
{
    Electronic = 0,
    ImmediateSpinIS = 1,
    AHGPhase = 2,
    Full = 3
}

// ─── Index entry ──────────────────────────────────────────────────────────────

/// <summary>Lightweight per-patient crossmatch index entry.</summary>
[GenerateSerializer]
public class CrossmatchIndexEntry
{
    [Id(0)]
    public string CrossmatchId { get; set; } = string.Empty;

    [Id(1)]
    public string UnitId { get; set; } = string.Empty;

    [Id(2)]
    public string ProductType { get; set; } = string.Empty;

    [Id(3)]
    public CrossmatchResult Result { get; set; }

    [Id(4)]
    public CrossmatchUrgency Urgency { get; set; }

    [Id(5)]
    public DateTime RequestedDate { get; set; }

    [Id(6)]
    public bool IsIssued { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Crossmatch State — a single crossmatch/compatibility test request record.
/// Maps to VistA BLOOD BANK CROSSMATCH file (#65.03).
/// </summary>
[GenerateSerializer]
public class CrossmatchState
{
    /// <summary>Unique crossmatch identifier (.01).</summary>
    [Id(0)]
    public string CrossmatchId { get; set; } = string.Empty;

    /// <summary>Patient this crossmatch is for (.02).</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Blood unit being tested (.03).</summary>
    [Id(2)]
    public string UnitId { get; set; } = string.Empty;

    /// <summary>Clinical urgency of the request (.04).</summary>
    [Id(3)]
    public CrossmatchUrgency Urgency { get; set; } = CrossmatchUrgency.Routine;

    /// <summary>Date/time the crossmatch was requested (.05).</summary>
    [Id(4)]
    public DateTime RequestedDate { get; set; }

    /// <summary>UserId who requested the crossmatch (.06).</summary>
    [Id(5)]
    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Name of the requesting clinician (.07).</summary>
    [Id(6)]
    public string RequestedByUserName { get; set; } = string.Empty;

    /// <summary>Patient's ABO type at time of request (.08) — for comparison.</summary>
    [Id(7)]
    public string? PatientAboType { get; set; }

    /// <summary>Patient's Rh type at time of request (.09).</summary>
    [Id(8)]
    public string? PatientRhType { get; set; }

    /// <summary>Unit ABO type (.10) — from the blood unit record.</summary>
    [Id(9)]
    public string? UnitAboType { get; set; }

    /// <summary>Unit Rh type (.11).</summary>
    [Id(10)]
    public string? UnitRhType { get; set; }

    /// <summary>Compatibility result (.12).</summary>
    [Id(11)]
    public CrossmatchResult Result { get; set; } = CrossmatchResult.Pending;

    /// <summary>Date/time the result was recorded (.13).</summary>
    [Id(12)]
    public DateTime? ResultDate { get; set; }

    /// <summary>Lab technician who performed the test (.14).</summary>
    [Id(13)]
    public string? TechnicianId { get; set; }

    /// <summary>Technician name (.15).</summary>
    [Id(14)]
    public string? TechnicianName { get; set; }

    /// <summary>Antibody identification if result was incompatible (.16).</summary>
    [Id(15)]
    public string? AntibodyIdentification { get; set; }

    /// <summary>Laboratory method used to perform the crossmatch (.17).</summary>
    [Id(16)]
    public CrossmatchMethod? CrossmatchMethod { get; set; }

    /// <summary>Date/time the unit was physically issued to the patient care area (.18).</summary>
    [Id(17)]
    public DateTime? IssuedDate { get; set; }

    /// <summary>UserId who issued the unit (.19).</summary>
    [Id(18)]
    public string? IssuedByUserId { get; set; }

    /// <summary>Name of person who issued the unit (.20).</summary>
    [Id(19)]
    public string? IssuedByUserName { get; set; }

    /// <summary>TransfusionId when the unit has been transfused (.21).</summary>
    [Id(20)]
    public string? TransfusionId { get; set; }

    /// <summary>Free-text notes.</summary>
    [Id(21)]
    public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(22)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this record was last modified.</summary>
    [Id(23)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
