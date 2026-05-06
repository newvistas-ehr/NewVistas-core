// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Means Test Billing Clock record (VistA File #351 MEANS TEST BILLING CLOCK).
/// Tracks the eligibility "clock" that determines when copay obligations begin and end
/// based on the patient's annual means test result and enrollment status.
///
/// When a patient completes a Means Test that shows copay obligation, a billing clock
/// starts. The clock drives which visits and prescriptions generate copay charges.
/// Grain key: "IB-CLOCK:{patientId}"
/// </summary>
[GenerateSerializer]
public class MeansTestBillingClockState
{
    /// <summary>Patient DFN/identifier this clock belongs to. (.01)</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Clock status: ACTIVE, EXPIRED, SUSPENDED, NOT STARTED.
    /// Controls whether copay charges are generated for this patient.
    /// </summary>
    [Id(1)] public string ClockStatus { get; set; } = "NOT STARTED";

    /// <summary>Date the billing obligation clock started. (.02)</summary>
    [Id(2)] public DateTime? ClockStartDate { get; set; }

    /// <summary>Date the current clock period expires. (.03)</summary>
    [Id(3)] public DateTime? ClockExpirationDate { get; set; }

    /// <summary>
    /// Identifier of the Means Test grain that triggered this clock.
    /// Links to IMeansTestGrain (File #408.31).
    /// </summary>
    [Id(4)] public string? MeansTestId { get; set; }

    /// <summary>Type of test that set this clock: MEANS TEST or COPAY TEST.</summary>
    [Id(5)] public string? TestType { get; set; }

    /// <summary>
    /// Eligibility determination from the linked means test:
    /// e.g., "COPAY REQUIRED", "EXEMPT", "HARDSHIP".
    /// </summary>
    [Id(6)] public string? EligibilityDetermination { get; set; }

    /// <summary>Date copay obligation begins (may differ from clock start due to grace periods).</summary>
    [Id(7)] public DateTime? CopayObligationStartDate { get; set; }

    /// <summary>Date copay obligation ends. Null = ongoing.</summary>
    [Id(8)] public DateTime? CopayObligationEndDate { get; set; }

    /// <summary>
    /// Billing category that applies during this clock period.
    /// Derived from enrollment priority group and means test outcome.
    /// </summary>
    [Id(9)] public string? BillingCategory { get; set; }

    /// <summary>Enrollment priority group (1–8) at the time the clock was set.</summary>
    [Id(10)] public string? PriorityGroup { get; set; }

    /// <summary>Date the clock was last reset.</summary>
    [Id(11)] public DateTime? LastResetDate { get; set; }

    /// <summary>Reason for the last clock reset (e.g., "NEW MEANS TEST COMPLETED", "ELIGIBILITY CHANGE").</summary>
    [Id(12)] public string? ResetReason { get; set; }

    /// <summary>Reason the clock is currently suspended (e.g., "APPEAL IN PROGRESS", "DATA ENTRY ERROR").</summary>
    [Id(13)] public string? SuspendedReason { get; set; }

    /// <summary>Free-text notes about this billing clock record.</summary>
    [Id(14)] public string? Notes { get; set; }

    /// <summary>Date this grain record was first created.</summary>
    [Id(15)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this grain record was last modified.</summary>
    [Id(16)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
