// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a mass casualty incident (MCI) event.
/// Tracks the incident lifecycle from activation through deactivation,
/// including triage counts, status updates, and after-action review.
/// </summary>
[GenerateSerializer]
public class MassCasualtyIncidentState
{
    /// <summary>Unique incident ID (grain key, e.g., "MCI:{guid}").</summary>
    [Id(0)]
    public string IncidentId { get; set; } = string.Empty;

    /// <summary>Incident name (e.g., "Highway 10 Multi-Vehicle Collision").</summary>
    [Id(1)]
    public string IncidentName { get; set; } = string.Empty;

    /// <summary>Incident type: MVC, EXPLOSION, SHOOTING, HAZMAT, NATURAL_DISASTER, STRUCTURAL_COLLAPSE, OTHER.</summary>
    [Id(2)]
    public string IncidentType { get; set; } = string.Empty;

    /// <summary>Severity: LEVEL_1 (10-20 casualties), LEVEL_2 (21-50), LEVEL_3 (51+).</summary>
    [Id(3)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>Status: ACTIVE, DEACTIVATED.</summary>
    [Id(4)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>Description of the incident.</summary>
    [Id(5)]
    public string? Description { get; set; }

    /// <summary>Estimated total casualties.</summary>
    [Id(6)]
    public int EstimatedCasualties { get; set; }

    /// <summary>Running count of registered casualties.</summary>
    [Id(7)]
    public int RegisteredCasualtyCount { get; set; }

    /// <summary>Who activated the MCI mode.</summary>
    [Id(8)]
    public string ActivatedByName { get; set; } = string.Empty;

    /// <summary>When MCI was activated.</summary>
    [Id(9)]
    public DateTime ActivatedDate { get; set; }

    /// <summary>When MCI was deactivated.</summary>
    [Id(10)]
    public DateTime? DeactivatedDate { get; set; }

    /// <summary>Who deactivated the MCI.</summary>
    [Id(11)]
    public string? DeactivatedByName { get; set; }

    /// <summary>After-action review notes.</summary>
    [Id(12)]
    public string? AfterActionNotes { get; set; }

    /// <summary>Chronological status updates during the incident.</summary>
    [Id(13)]
    public List<MciStatusUpdate> StatusUpdates { get; set; } = new();

    [Id(14)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(15)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

[GenerateSerializer]
public class MciStatusUpdate
{
    [Id(0)]
    public DateTime Timestamp { get; set; }

    [Id(1)]
    public string Message { get; set; } = string.Empty;

    [Id(2)]
    public string AuthorName { get; set; } = string.Empty;
}

/// <summary>
/// State for a single casualty tracked within a mass casualty incident.
/// Uses START triage categories (Simple Triage and Rapid Treatment).
/// </summary>
[GenerateSerializer]
public class MassCasualtyCasualtyState
{
    /// <summary>Unique casualty ID (grain key, e.g., "MCI-CASUALTY:{guid}").</summary>
    [Id(0)]
    public string CasualtyId { get; set; } = string.Empty;

    /// <summary>Parent incident ID.</summary>
    [Id(1)]
    public string IncidentId { get; set; } = string.Empty;

    /// <summary>Physical triage tag number (e.g., "T-0042").</summary>
    [Id(2)]
    public string TriageTag { get; set; } = string.Empty;

    /// <summary>START triage category: IMMEDIATE (red), DELAYED (yellow), MINOR (green), EXPECTANT (black).</summary>
    [Id(3)]
    public string TriageCategory { get; set; } = string.Empty;

    /// <summary>Linked patient ID (may be null if unidentified).</summary>
    [Id(4)]
    public string? PatientId { get; set; }

    /// <summary>Patient name (may be "UNIDENTIFIED" initially).</summary>
    [Id(5)]
    public string PatientName { get; set; } = "UNIDENTIFIED";

    /// <summary>Chief injury or complaint.</summary>
    [Id(6)]
    public string? ChiefInjury { get; set; }

    /// <summary>How the casualty arrived: AMBULANCE, WALK_IN, HELICOPTER, BUS, POLICE, OTHER.</summary>
    [Id(7)]
    public string? ArrivalMode { get; set; }

    /// <summary>Assigned treatment area: TRAUMA_BAY, RED_AREA, YELLOW_AREA, GREEN_AREA, EXPECTANT_AREA, OR, ICU.</summary>
    [Id(8)]
    public string? TreatmentArea { get; set; }

    /// <summary>Disposition: ADMITTED, TRANSFERRED, DISCHARGED, DECEASED, PENDING.</summary>
    [Id(9)]
    public string Disposition { get; set; } = "PENDING";

    /// <summary>Who registered this casualty.</summary>
    [Id(10)]
    public string RegisteredByName { get; set; } = string.Empty;

    /// <summary>Clinical notes.</summary>
    [Id(11)]
    public List<MciCasualtyNote> Notes { get; set; } = new();

    [Id(12)]
    public DateTime RegisteredDate { get; set; } = DateTime.UtcNow;

    [Id(13)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

[GenerateSerializer]
public class MciCasualtyNote
{
    [Id(0)]
    public DateTime Timestamp { get; set; }

    [Id(1)]
    public string Note { get; set; } = string.Empty;

    [Id(2)]
    public string AuthorName { get; set; } = string.Empty;
}

// ── Index entries ───────────────────────────────────────────────

[GenerateSerializer]
public class MassCasualtyIncidentIndexEntry
{
    [Id(0)]
    public string IncidentId { get; set; } = string.Empty;

    [Id(1)]
    public string IncidentName { get; set; } = string.Empty;

    [Id(2)]
    public string IncidentType { get; set; } = string.Empty;

    [Id(3)]
    public string Severity { get; set; } = string.Empty;

    [Id(4)]
    public string Status { get; set; } = string.Empty;

    [Id(5)]
    public int EstimatedCasualties { get; set; }

    [Id(6)]
    public int RegisteredCasualtyCount { get; set; }

    [Id(7)]
    public DateTime ActivatedDate { get; set; }
}

[GenerateSerializer]
public class MassCasualtyCasualtyIndexEntry
{
    [Id(0)]
    public string CasualtyId { get; set; } = string.Empty;

    [Id(1)]
    public string IncidentId { get; set; } = string.Empty;

    [Id(2)]
    public string TriageTag { get; set; } = string.Empty;

    [Id(3)]
    public string TriageCategory { get; set; } = string.Empty;

    [Id(4)]
    public string PatientName { get; set; } = string.Empty;

    [Id(5)]
    public string? TreatmentArea { get; set; }

    [Id(6)]
    public string Disposition { get; set; } = string.Empty;

    [Id(7)]
    public DateTime RegisteredDate { get; set; }
}

// ── Index states ────────────────────────────────────────────────

[GenerateSerializer]
public class MassCasualtyIncidentIndexState
{
    [Id(0)]
    public Dictionary<string, MassCasualtyIncidentIndexEntry> Entries { get; set; } = new();
}

[GenerateSerializer]
public class MassCasualtyCasualtyIndexState
{
    [Id(0)]
    public Dictionary<string, MassCasualtyCasualtyIndexEntry> Entries { get; set; } = new();
}
