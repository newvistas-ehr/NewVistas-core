// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary entry in the per-patient billing action index.
/// Used for fast list views without loading individual action grains.
/// </summary>
[GenerateSerializer]
public record IBillingActionIndexEntry
{
    /// <summary>Unique billing action identifier. Matches IBillingActionGrain key suffix.</summary>
    [Id(0)] public string BillingActionId { get; init; } = string.Empty;

    /// <summary>Patient this action belongs to.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>IB Action Type code (e.g., "PSO NSC RX COPAY NEW"). File #350.1.</summary>
    [Id(2)] public string ActionTypeCode { get; init; } = string.Empty;

    /// <summary>Human-readable action type description.</summary>
    [Id(3)] public string ActionTypeDescription { get; init; } = string.Empty;

    /// <summary>Current processing status. File #350.21.</summary>
    [Id(4)] public IBillingActionStatus Status { get; init; } = IBillingActionStatus.Incomplete;

    /// <summary>Dollar amount charged. Null if not yet calculated.</summary>
    [Id(5)] public decimal? ChargeAmount { get; init; }

    /// <summary>Date of the clinical service that generated this charge.</summary>
    [Id(6)] public DateTime ServiceDate { get; init; }

    /// <summary>Date this action was entered into the system.</summary>
    [Id(7)] public DateTime EnteredDate { get; init; }
}

/// <summary>
/// Per-patient index of all Integrated Billing actions (File #350).
/// Grain key: "IB-ACTION-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class IBillingActionIndexState
{
    /// <summary>Patient whose billing actions are indexed here.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All billing action entries for this patient, newest first.</summary>
    [Id(1)] public List<IBillingActionIndexEntry> Entries { get; set; } = new();
}
