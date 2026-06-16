// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// NCPDP transaction type — RPMS ABSP Field 103 (File #9002313.82103).
/// </summary>
[GenerateSerializer]
public enum NcpdpTransactionType
{
    B1 = 0,     // Billing request
    B2 = 1,     // Reversal
    B3 = 2,     // Rebill
    E1 = 3,     // Eligibility verification
    D1 = 4,     // Downtime billing
    P1 = 5,     // Prior authorization
}

/// <summary>
/// POS claim lifecycle status — RPMS ABSP transaction status.
/// </summary>
[GenerateSerializer]
public enum PosClaimStatus
{
    Pending = 0,
    Transmitted = 1,
    Paid = 2,
    Rejected = 3,
    Reversed = 4,
    DuplicatePaid = 5,
    PartialPay = 6,
    Cancelled = 7,
}

/// <summary>
/// DUR (Drug Utilization Review) conflict level — NCPDP Field 439.
/// </summary>
[GenerateSerializer]
public enum DurConflictLevel
{
    Informational = 0,
    Warning = 1,
    Critical = 2,
}

// ── Nested Entry Types ───────────────────────────────────────────────────────

/// <summary>
/// A DUR (Drug Utilization Review) message returned in claim response.
/// Maps to NCPDP DUR/PPS segment.
/// </summary>
[GenerateSerializer]
public class DurMessage
{
    /// <summary>DUR reason code (NCPDP Field 439).</summary>
    [Id(0)]
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>DUR clinical significance code.</summary>
    [Id(1)]
    public string? ClinicalSignificance { get; set; }

    /// <summary>Conflicting drug product ID (NDC).</summary>
    [Id(2)]
    public string? ConflictingDrugNdc { get; set; }

    /// <summary>Conflict level.</summary>
    [Id(3)]
    public DurConflictLevel Level { get; set; }

    /// <summary>Human-readable message.</summary>
    [Id(4)]
    public string? Message { get; set; }
}

/// <summary>
/// A rejection reason returned in claim response.
/// Maps to RPMS ABSP REJECT CODES (File #9002313.3).
/// </summary>
[GenerateSerializer]
public class PosRejection
{
    /// <summary>NCPDP rejection code (e.g., "79" = Refill Too Soon).</summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable rejection description.</summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Category: P=Pricing, B=Benefit, N=Network.</summary>
    [Id(2)]
    public string? Category { get; set; }
}

// ── Main State Classes ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for a Pharmacy POS Claim grain (POS-CLAIM:{id}).
/// Maps to RPMS ABSP CLAIMS (File #9002313.02) and ABSP RESPONSES (File #9002313.03).
/// Models a single NCPDP transaction (B1 billing, B2 reversal, E1 eligibility).
/// </summary>
[GenerateSerializer]
public class PharmacyPosClaimState
{
    /// <summary>Unique grain key (POS-CLAIM:{guid}).</summary>
    [Id(0)]
    public string ClaimId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Link to IPharmacyGrain prescription (optional for E1).</summary>
    [Id(2)]
    public string? PrescriptionId { get; set; }

    /// <summary>NCPDP transaction type (B1, B2, E1).</summary>
    [Id(3)]
    public NcpdpTransactionType TransactionType { get; set; }

    /// <summary>Claim lifecycle status.</summary>
    [Id(4)]
    public PosClaimStatus Status { get; set; }

    // ── NCPDP Header Segment ─────────────────────────────────────────────

    /// <summary>BIN — Bank Identification Number (Field 101, 6 digits).</summary>
    [Id(5)]
    public string Bin { get; set; } = string.Empty;

    /// <summary>PCN — Processor Control Number (Field 104).</summary>
    [Id(6)]
    public string Pcn { get; set; } = string.Empty;

    /// <summary>NCPDP version (Field 102, e.g., "D0", "51").</summary>
    [Id(7)]
    public string NcpdpVersion { get; set; } = string.Empty;

    // ── Insurance Segment ────────────────────────────────────────────────

    /// <summary>Group number from insurer.</summary>
    [Id(8)]
    public string? GroupNumber { get; set; }

    /// <summary>Cardholder ID (NCPDP Field 302).</summary>
    [Id(9)]
    public string? CardholderId { get; set; }

    /// <summary>Relationship to cardholder (1=self, 2=spouse, 3=child).</summary>
    [Id(10)]
    public string? RelationshipCode { get; set; }

    /// <summary>Insurer grain key for configuration lookup.</summary>
    [Id(11)]
    public string? InsurerId { get; set; }

    /// <summary>Insurer name for display.</summary>
    [Id(12)]
    public string? InsurerName { get; set; }

    // ── Drug / Prescription Segment ──────────────────────────────────────

    /// <summary>NDC (National Drug Code) — 11 digits.</summary>
    [Id(13)]
    public string? Ndc { get; set; }

    /// <summary>Drug name.</summary>
    [Id(14)]
    public string? DrugName { get; set; }

    /// <summary>Quantity dispensed.</summary>
    [Id(15)]
    public decimal? QuantityDispensed { get; set; }

    /// <summary>Days supply.</summary>
    [Id(16)]
    public int? DaysSupply { get; set; }

    /// <summary>Date of service (fill date).</summary>
    [Id(17)]
    public DateTime? DateOfService { get; set; }

    // ── Pricing Segment (Submitted) ──────────────────────────────────────

    /// <summary>Ingredient cost submitted.</summary>
    [Id(18)]
    public decimal? IngredientCostSubmitted { get; set; }

    /// <summary>Dispensing fee submitted.</summary>
    [Id(19)]
    public decimal? DispensingFeeSubmitted { get; set; }

    /// <summary>Usual and customary charge.</summary>
    [Id(20)]
    public decimal? UsualAndCustomary { get; set; }

    /// <summary>Gross amount due (ingredient cost + dispensing fee).</summary>
    [Id(21)]
    public decimal? GrossAmountDue { get; set; }

    // ── Response / Adjudication ──────────────────────────────────────────

    /// <summary>Amount paid by insurer.</summary>
    [Id(22)]
    public decimal? InsurancePaidAmount { get; set; }

    /// <summary>Patient responsibility (copay + coinsurance + deductible).</summary>
    [Id(23)]
    public decimal? PatientResponsibility { get; set; }

    /// <summary>Copay amount.</summary>
    [Id(24)]
    public decimal? CopayAmount { get; set; }

    /// <summary>Coinsurance amount.</summary>
    [Id(25)]
    public decimal? CoinsuranceAmount { get; set; }

    /// <summary>Deductible amount applied.</summary>
    [Id(26)]
    public decimal? DeductibleAmount { get; set; }

    /// <summary>Rejection reasons (if status is Rejected).</summary>
    [Id(27)]
    public List<PosRejection> Rejections { get; set; } = new();

    /// <summary>DUR messages returned in response.</summary>
    [Id(28)]
    public List<DurMessage> DurMessages { get; set; } = new();

    /// <summary>Payer authorization number (if approved).</summary>
    [Id(29)]
    public string? AuthorizationNumber { get; set; }

    // ── Provider / Pharmacy ──────────────────────────────────────────────

    /// <summary>Pharmacy NCPDP ID (Field 201).</summary>
    [Id(30)]
    public string? PharmacyNcpdpId { get; set; }

    /// <summary>Dispensing pharmacist name.</summary>
    [Id(31)]
    public string? PharmacistName { get; set; }

    /// <summary>Prescriber NPI.</summary>
    [Id(32)]
    public string? PrescriberNpi { get; set; }

    /// <summary>Prescriber name.</summary>
    [Id(33)]
    public string? PrescriberName { get; set; }

    // ── Reversal ─────────────────────────────────────────────────────────

    /// <summary>If this is a B2 reversal, the original B1 claim ID.</summary>
    [Id(34)]
    public string? OriginalClaimId { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────

    [Id(35)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(36)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persistent state for a Pharmacy POS Insurer grain (POS-INSURER:{id}).
/// Maps to RPMS ABSP INSURER (File #9002313.4).
/// </summary>
[GenerateSerializer]
public class PharmacyPosInsurerState
{
    [Id(0)] public string InsurerId { get; set; } = string.Empty;
    [Id(1)] public string InsurerName { get; set; } = string.Empty;
    [Id(2)] public string Bin { get; set; } = string.Empty;
    [Id(3)] public string Pcn { get; set; } = string.Empty;
    [Id(4)] public string NcpdpVersion { get; set; } = "D0";
    [Id(5)] public string? PharmacyNcpdpId { get; set; }
    [Id(6)] public string? ServiceProviderIdQualifier { get; set; }
    [Id(7)] public string? PlanName { get; set; }
    [Id(8)] public string? HelpDeskPhone { get; set; }
    [Id(9)] public bool IsActive { get; set; } = true;
    [Id(10)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(11)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index Entries ────────────────────────────────────────────────────────────

[GenerateSerializer]
public class PosClaimIndexEntry
{
    [Id(0)] public string ClaimId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public NcpdpTransactionType TransactionType { get; set; }
    [Id(3)] public PosClaimStatus Status { get; set; }
    [Id(4)] public string? DrugName { get; set; }
    [Id(5)] public DateTime? DateOfService { get; set; }
    [Id(6)] public decimal? InsurancePaidAmount { get; set; }
    [Id(7)] public decimal? PatientResponsibility { get; set; }
    [Id(8)] public string? InsurerName { get; set; }
}

[GenerateSerializer]
public class PosClaimIndexState
{
    [Id(0)] public List<PosClaimIndexEntry> Entries { get; set; } = new();
}

[GenerateSerializer]
public class PosInsurerIndexEntry
{
    [Id(0)] public string InsurerId { get; set; } = string.Empty;
    [Id(1)] public string InsurerName { get; set; } = string.Empty;
    [Id(2)] public string Bin { get; set; } = string.Empty;
    [Id(3)] public string Pcn { get; set; } = string.Empty;
    [Id(4)] public bool IsActive { get; set; }
}

[GenerateSerializer]
public class PosInsurerIndexState
{
    [Id(0)] public List<PosInsurerIndexEntry> Entries { get; set; } = new();
}
