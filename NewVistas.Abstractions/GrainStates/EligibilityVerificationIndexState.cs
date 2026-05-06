// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary entry for the per-patient eligibility verification index.
/// </summary>
[GenerateSerializer]
public record EligibilityVerificationIndexEntry
{
    /// <summary>Eligibility inquiry ID (grain key).</summary>
    [Id(0)] public string InquiryId { get; init; } = string.Empty;

    /// <summary>Payer name for display.</summary>
    [Id(1)] public string PayerName { get; init; } = string.Empty;

    /// <summary>Subscriber / member ID.</summary>
    [Id(2)] public string SubscriberId { get; init; } = string.Empty;

    /// <summary>Date of service being verified.</summary>
    [Id(3)] public DateTime ServiceDate { get; init; }

    /// <summary>Current status of the inquiry.</summary>
    [Id(4)] public EligibilityInquiryStatus Status { get; init; }

    /// <summary>Coverage level from the 271 response (once received).</summary>
    [Id(5)] public CoverageLevel CoverageLevel { get; init; }

    /// <summary>Date the inquiry was submitted.</summary>
    [Id(6)] public DateTime? SubmittedDate { get; init; }

    /// <summary>Date the response was received.</summary>
    [Id(7)] public DateTime? ResponseDate { get; init; }
}

/// <summary>
/// Per-patient index of eligibility verification inquiries (EDI 270/271).
/// Grain key: "ELIG-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class EligibilityVerificationIndexState
{
    /// <summary>Patient this index belongs to.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All eligibility verification entries for this patient, newest first.</summary>
    [Id(1)] public List<EligibilityVerificationIndexEntry> Entries { get; set; } = new();
}
