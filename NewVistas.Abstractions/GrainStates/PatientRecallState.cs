// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a patient recall entry.
/// Maps to IHS RPMS SC Recall (File #403.5) for tracking patients
/// who are due or overdue for follow-up appointments, generating
/// recall letters and tracking contact attempts.
/// </summary>
[GenerateSerializer]
public class PatientRecallState
{
    /// <summary>Unique recall entry ID (grain key, e.g., "SD-RECALL:{guid}").</summary>
    [Id(0)]
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Patient due for recall.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Clinic for the follow-up appointment.</summary>
    [Id(3)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name for display.</summary>
    [Id(4)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Recall type: FOLLOW-UP, ANNUAL_EXAM, LAB_RECHECK, CHRONIC_CARE, IMMUNIZATION, SCREENING, PROCEDURE.</summary>
    [Id(5)]
    public string RecallType { get; set; } = string.Empty;

    /// <summary>Date the patient is due for the follow-up.</summary>
    [Id(6)]
    public DateTime RecallDate { get; set; }

    /// <summary>Status: PENDING, LETTER_SENT, CONTACTED, APPOINTMENT_SCHEDULED, COMPLETED, CANCELLED, OVERDUE.</summary>
    [Id(7)]
    public string Status { get; set; } = "PENDING";

    /// <summary>Provider responsible for this patient's recall.</summary>
    [Id(8)]
    public string? ProviderId { get; set; }

    /// <summary>Provider name for display.</summary>
    [Id(9)]
    public string? ProviderName { get; set; }

    /// <summary>Diagnosis or reason for recall.</summary>
    [Id(10)]
    public string? Diagnosis { get; set; }

    /// <summary>Special instructions for the follow-up.</summary>
    [Id(11)]
    public string? Instructions { get; set; }

    /// <summary>Provider who created the recall entry.</summary>
    [Id(12)]
    public string CreatedByProviderId { get; set; } = string.Empty;

    /// <summary>Name of provider who created the recall entry.</summary>
    [Id(13)]
    public string CreatedByProviderName { get; set; } = string.Empty;

    /// <summary>History of letters generated for this recall.</summary>
    [Id(14)]
    public List<RecallLetterEntry> Letters { get; set; } = new();

    /// <summary>History of contact attempts (phone, mail, etc.).</summary>
    [Id(15)]
    public List<RecallContactAttempt> ContactAttempts { get; set; } = new();

    /// <summary>Appointment ID if scheduled from recall.</summary>
    [Id(16)]
    public string? ScheduledAppointmentId { get; set; }

    /// <summary>Scheduled appointment date/time.</summary>
    [Id(17)]
    public DateTime? ScheduledAppointmentDateTime { get; set; }

    /// <summary>Who scheduled the appointment.</summary>
    [Id(18)]
    public string? ScheduledByName { get; set; }

    /// <summary>Cancellation reason.</summary>
    [Id(19)]
    public string? CancellationReason { get; set; }

    /// <summary>Completion notes.</summary>
    [Id(20)]
    public string? CompletionNotes { get; set; }

    /// <summary>Number of letters sent.</summary>
    [Id(21)]
    public int LetterCount { get; set; }

    /// <summary>Number of contact attempts.</summary>
    [Id(22)]
    public int ContactAttemptCount { get; set; }

    [Id(23)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(24)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Record of a recall letter generated for a patient.
/// </summary>
[GenerateSerializer]
public class RecallLetterEntry
{
    [Id(0)]
    public DateTime GeneratedDate { get; set; }

    [Id(1)]
    public string LetterType { get; set; } = string.Empty;

    [Id(2)]
    public string GeneratedByName { get; set; } = string.Empty;
}

/// <summary>
/// Record of a contact attempt for a recall patient.
/// </summary>
[GenerateSerializer]
public class RecallContactAttempt
{
    [Id(0)]
    public DateTime AttemptDate { get; set; }

    /// <summary>Contact method: PHONE, LETTER, SECURE_MESSAGE, IN_PERSON.</summary>
    [Id(1)]
    public string ContactMethod { get; set; } = string.Empty;

    /// <summary>Result: REACHED, NO_ANSWER, LEFT_MESSAGE, WRONG_NUMBER, RETURNED_MAIL.</summary>
    [Id(2)]
    public string Result { get; set; } = string.Empty;

    [Id(3)]
    public string ContactedByName { get; set; } = string.Empty;

    [Id(4)]
    public string? Notes { get; set; }
}

/// <summary>
/// Index entry for the system-level patient recall index.
/// </summary>
[GenerateSerializer]
public class PatientRecallIndexEntry
{
    [Id(0)]
    public string EntryId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string ClinicId { get; set; } = string.Empty;

    [Id(4)]
    public string ClinicName { get; set; } = string.Empty;

    [Id(5)]
    public string RecallType { get; set; } = string.Empty;

    [Id(6)]
    public DateTime RecallDate { get; set; }

    [Id(7)]
    public string Status { get; set; } = string.Empty;

    [Id(8)]
    public string? ProviderName { get; set; }

    [Id(9)]
    public int LetterCount { get; set; }

    [Id(10)]
    public int ContactAttemptCount { get; set; }
}

/// <summary>
/// Persistent state for the patient recall index singleton.
/// </summary>
[GenerateSerializer]
public class PatientRecallIndexState
{
    [Id(0)]
    public Dictionary<string, PatientRecallIndexEntry> Entries { get; set; } = new();
}
