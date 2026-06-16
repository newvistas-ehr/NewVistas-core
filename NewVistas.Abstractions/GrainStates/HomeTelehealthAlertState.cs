// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// An alert generated when a patient reading falls outside defined thresholds.
/// Based on VistA HOME TELEHEALTH ALERT file (#720.9).
/// </summary>
[GenerateSerializer]
public class HomeTelehealthAlertState
{
    /// <summary>
    /// Unique alert identifier (.01).
    /// </summary>
    [Id(0)]
    public string AlertId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (.02) — pointer to PATIENT file (#2).
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// The reading that triggered this alert (.03).
    /// </summary>
    [Id(2)]
    public string ReadingId { get; set; } = string.Empty;

    /// <summary>
    /// Type of measurement that triggered the alert (.04).
    /// </summary>
    [Id(3)]
    public HtMeasurementType MeasurementType { get; set; }

    /// <summary>
    /// Primary out-of-range value (.05).
    /// </summary>
    [Id(4)]
    public decimal? Value1 { get; set; }

    /// <summary>
    /// Secondary out-of-range value (.06) (e.g. diastolic BP).
    /// </summary>
    [Id(5)]
    public decimal? Value2 { get; set; }

    /// <summary>
    /// Clinical severity of the alert (.07).
    /// </summary>
    [Id(6)]
    public HtAlertSeverity Severity { get; set; }

    /// <summary>
    /// Human-readable description of why this alert was triggered (.08).
    /// </summary>
    [Id(7)]
    public string AlertDescription { get; set; } = string.Empty;

    /// <summary>
    /// Current alert workflow status (.09).
    /// </summary>
    [Id(8)]
    public HtAlertStatus Status { get; set; }

    /// <summary>
    /// Date and time the alert was generated (.10).
    /// </summary>
    [Id(9)]
    public DateTime AlertDateTime { get; set; }

    /// <summary>
    /// ID of the clinician who acknowledged this alert (.11).
    /// </summary>
    [Id(10)]
    public string? AcknowledgedById { get; set; }

    /// <summary>
    /// Name of the clinician who acknowledged this alert (.12).
    /// </summary>
    [Id(11)]
    public string? AcknowledgedByName { get; set; }

    /// <summary>
    /// Date/time the alert was acknowledged (.13).
    /// </summary>
    [Id(12)]
    public DateTime? AcknowledgedDate { get; set; }

    /// <summary>
    /// Clinical response or action taken (.14).
    /// </summary>
    [Id(13)]
    public string? ClinicalResponse { get; set; }

    /// <summary>
    /// Date this record was created.
    /// </summary>
    [Id(14)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date this record was last modified.
    /// </summary>
    [Id(15)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Clinical severity of a Home Telehealth alert.
/// </summary>
[GenerateSerializer]
public enum HtAlertSeverity
{
    Low = 0,
    Moderate = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Workflow status of a Home Telehealth alert.
/// </summary>
[GenerateSerializer]
public enum HtAlertStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2,
    Dismissed = 3
}

/// <summary>
/// Summary entry stored in the patient alert index.
/// </summary>
[GenerateSerializer]
public class HtAlertIndexEntry
{
    /// <summary>Alert grain key.</summary>
    [Id(0)]
    public string AlertId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Reading that triggered this alert.</summary>
    [Id(2)]
    public string ReadingId { get; set; } = string.Empty;

    /// <summary>Measurement type.</summary>
    [Id(3)]
    public HtMeasurementType MeasurementType { get; set; }

    /// <summary>Clinical severity.</summary>
    [Id(4)]
    public HtAlertSeverity Severity { get; set; }

    /// <summary>Current workflow status.</summary>
    [Id(5)]
    public HtAlertStatus Status { get; set; }

    /// <summary>Date and time the alert was generated.</summary>
    [Id(6)]
    public DateTime AlertDateTime { get; set; }

    /// <summary>Human-readable alert description.</summary>
    [Id(7)]
    public string AlertDescription { get; set; } = string.Empty;
}

/// <summary>
/// Index of all alerts for a single patient.
/// Grain key: "HT-ALERT-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class HomeTelehealthAlertIndexState
{
    /// <summary>
    /// All alert summaries for this patient, most recent first.
    /// </summary>
    [Id(0)]
    public List<HtAlertIndexEntry> Entries { get; set; } = new();
}
