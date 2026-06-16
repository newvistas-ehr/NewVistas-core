// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a structured anesthesia record during surgery.
/// Extends VistA Surgery (File #130) which only captures anesthesia type.
/// Provides full intraoperative anesthesia documentation.
/// </summary>
[GenerateSerializer]
public class AnesthesiaRecordState
{
    /// <summary>Unique record ID (grain key, e.g., "ANES:{guid}").</summary>
    [Id(0)]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>Patient undergoing anesthesia.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Linked surgery case ID (VistA File #130 reference).</summary>
    [Id(3)]
    public string SurgeryId { get; set; } = string.Empty;

    /// <summary>Procedure name.</summary>
    [Id(4)]
    public string ProcedureName { get; set; } = string.Empty;

    /// <summary>Anesthesia type: GENERAL, REGIONAL, LOCAL, MAC, SPINAL, EPIDURAL, COMBINED.</summary>
    [Id(5)]
    public string AnesthesiaType { get; set; } = string.Empty;

    /// <summary>Anesthesiologist ID.</summary>
    [Id(6)]
    public string AnesthesiologistId { get; set; } = string.Empty;

    /// <summary>Anesthesiologist name.</summary>
    [Id(7)]
    public string AnesthesiologistName { get; set; } = string.Empty;

    /// <summary>ASA Physical Status: ASA_I, ASA_II, ASA_III, ASA_IV, ASA_V, ASA_VI.</summary>
    [Id(8)]
    public string AsaClassification { get; set; } = string.Empty;

    /// <summary>Mallampati airway class: CLASS_I, CLASS_II, CLASS_III, CLASS_IV.</summary>
    [Id(9)]
    public string? AirwayClass { get; set; }

    /// <summary>Pre-operative assessment notes.</summary>
    [Id(10)]
    public string? PreOpNotes { get; set; }

    /// <summary>Status: DRAFT, IN_PROGRESS, FINALIZED, ADDENDED.</summary>
    [Id(11)]
    public string Status { get; set; } = "DRAFT";

    /// <summary>Anesthetic agents administered.</summary>
    [Id(12)]
    public List<AnesthesiaAgent> Agents { get; set; } = new();

    /// <summary>Airway management details.</summary>
    [Id(13)]
    public string? AirwayDevice { get; set; }

    /// <summary>Airway device size (e.g., "7.0 ETT").</summary>
    [Id(14)]
    public string? AirwaySize { get; set; }

    /// <summary>Airway management notes.</summary>
    [Id(15)]
    public string? AirwayNotes { get; set; }

    /// <summary>Intraoperative vital sign readings.</summary>
    [Id(16)]
    public List<AnesthesiaVitalEntry> VitalEntries { get; set; } = new();

    /// <summary>Intraoperative events (intubation, line placement, etc.).</summary>
    [Id(17)]
    public List<AnesthesiaEvent> Events { get; set; } = new();

    /// <summary>Induction time.</summary>
    [Id(18)]
    public DateTime? InductionTime { get; set; }

    /// <summary>Induction method.</summary>
    [Id(19)]
    public string? InductionMethod { get; set; }

    /// <summary>Emergence time.</summary>
    [Id(20)]
    public DateTime? EmergenceTime { get; set; }

    /// <summary>Emergence notes.</summary>
    [Id(21)]
    public string? EmergenceNotes { get; set; }

    /// <summary>PACU handoff nurse.</summary>
    [Id(22)]
    public string? PacuNurse { get; set; }

    /// <summary>Aldrete score at PACU handoff (0-10).</summary>
    [Id(23)]
    public int? AldretScore { get; set; }

    /// <summary>PACU handoff notes.</summary>
    [Id(24)]
    public string? PacuHandoffNotes { get; set; }

    /// <summary>Addendum notes.</summary>
    [Id(25)]
    public string? AddendumNotes { get; set; }

    [Id(26)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(27)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An anesthetic agent administered during the case.
/// </summary>
[GenerateSerializer]
public class AnesthesiaAgent
{
    /// <summary>Agent name (e.g., "Propofol", "Sevoflurane", "Fentanyl").</summary>
    [Id(0)]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Category: INDUCTION, MAINTENANCE, ANALGESIC, PARALYTIC, REVERSAL, VASOPRESSOR, ANTIEMETIC, OTHER.</summary>
    [Id(1)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Dose (e.g., "200", "2%", "100").</summary>
    [Id(2)]
    public string Dose { get; set; } = string.Empty;

    /// <summary>Unit (e.g., "mg", "mcg", "mL", "%").</summary>
    [Id(3)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>Route: IV, INHALATION, INTRATHECAL, EPIDURAL, IM, TOPICAL.</summary>
    [Id(4)]
    public string Route { get; set; } = string.Empty;

    /// <summary>Time administered.</summary>
    [Id(5)]
    public DateTime AdministeredTime { get; set; }
}

/// <summary>
/// Intraoperative vital sign reading at a point in time.
/// </summary>
[GenerateSerializer]
public class AnesthesiaVitalEntry
{
    [Id(0)]
    public DateTime Timestamp { get; set; }

    [Id(1)]
    public int? SystolicBp { get; set; }

    [Id(2)]
    public int? DiastolicBp { get; set; }

    [Id(3)]
    public int? HeartRate { get; set; }

    [Id(4)]
    public int? SpO2 { get; set; }

    [Id(5)]
    public int? EtCo2 { get; set; }

    [Id(6)]
    public decimal? Temperature { get; set; }

    [Id(7)]
    public int? RespiratoryRate { get; set; }
}

/// <summary>
/// An intraoperative event logged during anesthesia.
/// </summary>
[GenerateSerializer]
public class AnesthesiaEvent
{
    [Id(0)]
    public DateTime Timestamp { get; set; }

    /// <summary>Event type: INTUBATION, EXTUBATION, LINE_PLACED, BLOOD_LOSS, TRANSFUSION, COMPLICATION, POSITION_CHANGE, NOTE.</summary>
    [Id(1)]
    public string EventType { get; set; } = string.Empty;

    [Id(2)]
    public string Description { get; set; } = string.Empty;

    [Id(3)]
    public string RecordedByName { get; set; } = string.Empty;
}

// ── Index ───────────────────────────────────────────────────────

[GenerateSerializer]
public class AnesthesiaRecordIndexEntry
{
    [Id(0)]
    public string RecordId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string ProcedureName { get; set; } = string.Empty;

    [Id(4)]
    public string AnesthesiaType { get; set; } = string.Empty;

    [Id(5)]
    public string AnesthesiologistName { get; set; } = string.Empty;

    [Id(6)]
    public string AnesthesiologistId { get; set; } = string.Empty;

    [Id(7)]
    public string AsaClassification { get; set; } = string.Empty;

    [Id(8)]
    public string Status { get; set; } = string.Empty;

    [Id(9)]
    public int AgentCount { get; set; }

    [Id(10)]
    public DateTime CreatedDate { get; set; }
}

[GenerateSerializer]
public class AnesthesiaRecordIndexState
{
    [Id(0)]
    public Dictionary<string, AnesthesiaRecordIndexEntry> Entries { get; set; } = new();
}
