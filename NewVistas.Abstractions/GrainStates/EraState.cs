// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Processing status of an Electronic Remittance Advice (835).
/// </summary>
[GenerateSerializer]
public enum EraStatus
{
    /// <summary>ERA received but payments not yet posted to AR.</summary>
    Received = 0,

    /// <summary>All claim payments have been posted to AR accounts.</summary>
    Posted = 1,

    /// <summary>An error occurred during payment posting.</summary>
    Error = 2,
}

/// <summary>
/// Payment detail for a single claim within an ERA (835 CLP/SVC loop).
/// </summary>
[GenerateSerializer]
public record EraClaimPayment
{
    /// <summary>EDI claim grain key string that this payment applies to.</summary>
    [Id(0)] public string ClaimId { get; init; } = string.Empty;

    /// <summary>Patient associated with this claim payment.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>AR account to credit with this payment.</summary>
    [Id(2)] public string? ARAccountId { get; init; }

    /// <summary>Amount paid by the payer for this claim.</summary>
    [Id(3)] public decimal PaidAmount { get; init; }

    /// <summary>Amount allowed by the payer (contractual rate).</summary>
    [Id(4)] public decimal? AllowedAmount { get; init; }

    /// <summary>Contractual or other adjustment amount (reduction from billed to allowed).</summary>
    [Id(5)] public decimal? AdjustmentAmount { get; init; }

    /// <summary>CARC/RARC denial reason code (null if claim paid).</summary>
    [Id(6)] public string? DenialReasonCode { get; init; }

    /// <summary>Human-readable denial reason description.</summary>
    [Id(7)] public string? DenialReasonDescription { get; init; }
}

/// <summary>
/// State for an Electronic Remittance Advice (X12 835) received from a payer.
/// Maps to VistA File #364 (ELECTRONIC REMITTANCE ADVICE).
/// </summary>
[GenerateSerializer]
public class EraState
{
    /// <summary>Unique identifier for this ERA — grain key string.</summary>
    [Id(0)] public string EraId { get; set; } = string.Empty;

    /// <summary>Payer (insurance company) identifier.</summary>
    [Id(1)] public string PayerId { get; set; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(2)] public string PayerName { get; set; } = string.Empty;

    /// <summary>Check number or EFT trace number for the payment.</summary>
    [Id(3)] public string? CheckNumber { get; set; }

    /// <summary>Payment method (e.g., "ACH", "Check", "EFT").</summary>
    [Id(4)] public string? PaymentMethod { get; set; }

    /// <summary>Date the payer issued the payment.</summary>
    [Id(5)] public DateTime PaymentDate { get; set; }

    /// <summary>Total payment amount across all claims in this ERA.</summary>
    [Id(6)] public decimal TotalPaymentAmount { get; set; }

    /// <summary>X12 835 transaction set control number.</summary>
    [Id(7)] public string? TransactionSetId { get; set; }

    /// <summary>Individual claim payment details (CLP loops).</summary>
    [Id(8)] public List<EraClaimPayment> ClaimPayments { get; set; } = new();

    /// <summary>Current processing status of this ERA.</summary>
    [Id(9)] public EraStatus Status { get; set; }

    /// <summary>Date all payments were successfully posted to AR.</summary>
    [Id(10)] public DateTime? ProcessedDate { get; set; }

    /// <summary>Error message if posting failed.</summary>
    [Id(11)] public string? ErrorMessage { get; set; }

    /// <summary>Optional free-text notes.</summary>
    [Id(12)] public string? Notes { get; set; }

    /// <summary>When this ERA record was created.</summary>
    [Id(13)] public DateTime CreatedDate { get; set; }

    /// <summary>When this ERA record was last modified.</summary>
    [Id(14)] public DateTime LastModifiedDate { get; set; }
}
