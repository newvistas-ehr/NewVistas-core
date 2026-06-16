// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Summary of a single copay transaction posted to the patient's copay account.
/// Derived from IB billing action records (File #354.71 IB COPAY TRANSACTIONS).
/// </summary>
[GenerateSerializer]
public record CopayTransactionSummary
{
    /// <summary>Billing action that generated this transaction.</summary>
    [Id(0)] public string BillingActionId { get; init; } = string.Empty;

    /// <summary>Human-readable description of the action type.</summary>
    [Id(1)] public string Description { get; init; } = string.Empty;

    /// <summary>Dollar amount of this transaction.</summary>
    [Id(2)] public decimal Amount { get; init; }

    /// <summary>Date of the clinical service that generated this charge.</summary>
    [Id(3)] public DateTime ServiceDate { get; init; }

    /// <summary>Whether this transaction was exempted from copay obligation.</summary>
    [Id(4)] public bool IsExempt { get; init; }
}

/// <summary>
/// Patient billing / copay account record (VistA File #354 BILLING PATIENT and #354.7 IB PATIENT COPAY ACCOUNT).
/// Tracks exemption status, copay caps, hardship determinations, and year-to-date copay balance.
/// Grain key: "IB-PATIENT:{patientId}"
/// </summary>
[GenerateSerializer]
public class IBillingPatientState
{
    /// <summary>Patient DFN/identifier. (.01)</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Unique copay account identifier assigned at initialization.</summary>
    [Id(1)] public string CopayAccountId { get; set; } = string.Empty;

    /// <summary>Whether the patient is currently exempt from all copay obligations. (.02)</summary>
    [Id(2)] public bool IsExemptFromCopay { get; set; }

    /// <summary>
    /// Reason code for copay exemption (e.g., "SERVICE CONNECTED", "INCOME EXEMPT",
    /// "FORMER POW", "UNEMPLOYABLE VETERAN"). File #354.2 EXEMPTION REASON.
    /// </summary>
    [Id(3)] public string? ExemptionReasonCode { get; set; }

    /// <summary>Date the current exemption became effective. (.03)</summary>
    [Id(4)] public DateTime? ExemptionEffectiveDate { get; set; }

    /// <summary>Date the current exemption expires. Null = indefinite.</summary>
    [Id(5)] public DateTime? ExemptionExpirationDate { get; set; }

    /// <summary>
    /// Billing category code (A/B/C etc.) determining copay tier.
    /// Derived from means test result and service-connected status.
    /// </summary>
    [Id(6)] public string? CopayCategory { get; set; }

    /// <summary>Running year-to-date copay balance (dollar amount charged this calendar year). (.04)</summary>
    [Id(7)] public decimal CurrentYearCopayBalance { get; set; }

    /// <summary>Annual copay cap amount. When reached, further charges are exempt. File #354.75 IB COPAY CAPS.</summary>
    [Id(8)] public decimal? AnnualCopayCapAmount { get; set; }

    /// <summary>Date the annual copay cap was reached for the current year. Null if not yet reached.</summary>
    [Id(9)] public DateTime? CopayCapReachedDate { get; set; }

    /// <summary>
    /// Treatment category code indicating service-connected percentage and eligibility tier.
    /// Examples: "SC", "NSC", "AO", "IR", "SWAC".
    /// </summary>
    [Id(10)] public string? TreatmentCategoryCode { get; set; }

    /// <summary>VA enrollment priority group (1–8). Drives copay obligation tier.</summary>
    [Id(11)] public string? PriorityGroup { get; set; }

    /// <summary>Whether a financial hardship waiver is currently in effect. File #354.3 BILLING THRESHOLDS.</summary>
    [Id(12)] public bool IsHardshipGranted { get; set; }

    /// <summary>Date hardship was granted.</summary>
    [Id(13)] public DateTime? HardshipGrantedDate { get; set; }

    /// <summary>Date hardship waiver expires. Null = until next means test review.</summary>
    [Id(14)] public DateTime? HardshipExpirationDate { get; set; }

    /// <summary>Whether the patient is registered as catastrophically disabled (exempt from copay).</summary>
    [Id(15)] public bool CatastrophicallyDisabled { get; set; }

    /// <summary>Year-to-date copay transaction history (newest first, capped at last 100 entries).</summary>
    [Id(16)] public List<CopayTransactionSummary> YearToDateCopayTransactions { get; set; } = new();

    /// <summary>Date this grain record was first created.</summary>
    [Id(17)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this grain record was last modified.</summary>
    [Id(18)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
