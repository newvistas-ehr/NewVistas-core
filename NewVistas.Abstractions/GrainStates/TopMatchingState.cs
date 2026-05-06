// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Status of a TOP offset matching attempt.
/// </summary>
[GenerateSerializer]
public enum TopMatchStatus
{
    /// <summary>Offset received from Treasury, pending matching to AR account.</summary>
    PendingMatch = 0,
    /// <summary>Offset matched to an AR account and payment posted.</summary>
    Matched = 1,
    /// <summary>No matching AR account found; offset returned to Treasury.</summary>
    Unmatched = 2,
    /// <summary>Partial match — offset split across multiple accounts.</summary>
    PartialMatch = 3,
    /// <summary>Match rejected due to data discrepancy.</summary>
    Rejected = 4,
}

/// <summary>
/// Record of a single TOP offset payment received from Treasury and its matching outcome.
/// Maps to VistA PRCA TOP processing (RCTP*.m, RCTOP*.m).
/// Grain key: "TOP-MATCH:{guid}"
/// </summary>
[GenerateSerializer]
public class TopMatchingState
{
    /// <summary>Unique match record ID — grain key.</summary>
    [Id(0)] public string MatchId { get; set; } = string.Empty;

    /// <summary>Treasury Offset transaction reference number.</summary>
    [Id(1)] public string TreasuryTransactionId { get; set; } = string.Empty;

    /// <summary>Patient SSN or TIN used for matching.</summary>
    [Id(2)] public string TaxpayerIdNumber { get; set; } = string.Empty;

    /// <summary>Patient name from Treasury offset record.</summary>
    [Id(3)] public string TreasuryPatientName { get; set; } = string.Empty;

    /// <summary>Matched patient ID (set after successful match).</summary>
    [Id(4)] public string? MatchedPatientId { get; set; }

    /// <summary>Matched AR account ID (set after match).</summary>
    [Id(5)] public string? MatchedARAccountId { get; set; }

    /// <summary>Matched TOP referral ID (set after match).</summary>
    [Id(6)] public string? MatchedTopReferralId { get; set; }

    /// <summary>Amount offset by Treasury.</summary>
    [Id(7)] public decimal OffsetAmount { get; set; }

    /// <summary>Amount applied to AR account(s).</summary>
    [Id(8)] public decimal AppliedAmount { get; set; }

    /// <summary>Remaining amount not matched (OffsetAmount - AppliedAmount).</summary>
    [Id(9)] public decimal UnmatchedAmount { get; set; }

    /// <summary>Source of the offset (TAX_REFUND, SSA, OPM, VENDOR, etc.).</summary>
    [Id(10)] public string OffsetSource { get; set; } = string.Empty;

    /// <summary>Date the offset was received from Treasury.</summary>
    [Id(11)] public DateTime OffsetReceivedDate { get; set; }

    /// <summary>Date the match was processed.</summary>
    [Id(12)] public DateTime? MatchProcessedDate { get; set; }

    /// <summary>Current matching status.</summary>
    [Id(13)] public TopMatchStatus Status { get; set; }

    /// <summary>Reasons for match failure or rejection.</summary>
    [Id(14)] public List<string> MatchReasons { get; set; } = new();

    /// <summary>User who processed the match (null for auto-match).</summary>
    [Id(15)] public string? ProcessedByUserId { get; set; }

    /// <summary>Display name.</summary>
    [Id(16)] public string? ProcessedByUserName { get; set; }

    /// <summary>Optional notes.</summary>
    [Id(17)] public string? Notes { get; set; }

    /// <summary>When created.</summary>
    [Id(18)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(19)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight index entry for TOP match records.
/// </summary>
[GenerateSerializer]
public record TopMatchIndexEntry
{
    [Id(0)] public string MatchId { get; init; } = string.Empty;
    [Id(1)] public string TreasuryTransactionId { get; init; } = string.Empty;
    [Id(2)] public string TaxpayerIdNumber { get; init; } = string.Empty;
    [Id(3)] public string? MatchedPatientId { get; init; }
    [Id(4)] public decimal OffsetAmount { get; init; }
    [Id(5)] public TopMatchStatus Status { get; init; }
    [Id(6)] public string OffsetSource { get; init; } = string.Empty;
    [Id(7)] public DateTime OffsetReceivedDate { get; init; }
}

/// <summary>
/// Index state for TOP matching records.
/// Grain key: "TOP-MATCH-IDX" (singleton) or "TOP-MATCH-IDX:{patientId}" (per-patient)
/// </summary>
[GenerateSerializer]
public class TopMatchIndexState
{
    [Id(0)] public List<TopMatchIndexEntry> Entries { get; set; } = new();
}
