// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee Basis Patient — VistA File #162 FEE BASIS PATIENT ────────────────────

/// <summary>
/// Per-patient fee basis summary record tracking community care eligibility and
/// aggregate spending (VistA File #162 FEE BASIS PATIENT).
/// Managed by FBPAID.m, FBSVBR.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class FeePatientState
{
    /// <summary>Patient identifier this record belongs to.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Whether this patient is currently eligible for fee basis community care.</summary>
    [Id(1)] public bool IsEligibleForFeeBasis { get; set; }

    /// <summary>Date from which fee basis eligibility is effective (optional).</summary>
    [Id(2)] public DateTime? EligibilityStartDate { get; set; }

    /// <summary>Date through which fee basis eligibility is valid (optional).</summary>
    [Id(3)] public DateTime? EligibilityEndDate { get; set; }

    /// <summary>Sum of all authorized amounts across all active authorizations.</summary>
    [Id(4)] public decimal TotalAuthorizedAmount { get; set; }

    /// <summary>Sum of all paid invoice amounts across all authorizations.</summary>
    [Id(5)] public decimal TotalPaidAmount { get; set; }

    /// <summary>Count of authorizations with Active or Pending status.</summary>
    [Id(6)] public int ActiveAuthorizationCount { get; set; }

    /// <summary>Date and time of the most recent fee basis activity (auth or payment).</summary>
    [Id(7)] public DateTime? LastActivityDate { get; set; }

    /// <summary>Free-text notes about this patient's fee basis record.</summary>
    [Id(8)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(9)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(10)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fee basis category code (VistA File #162, Field #.07 CATEGORY OF CARE).
    /// E.g., "38 CFR 17.38", "Unusual Medical", "Humanitarian", "Philippine".
    /// </summary>
    [Id(11)] public string? FeeCategory { get; set; }

    /// <summary>
    /// Human-readable eligibility status string (e.g., "Eligible", "Ineligible", "Pending").
    /// Complements the boolean <see cref="IsEligibleForFeeBasis"/> for display purposes.
    /// </summary>
    [Id(12)] public string? EligibilityStatus { get; set; }

    /// <summary>
    /// Date the patient was enrolled in fee basis / community care (VistA File #162, Field #.08).
    /// Distinct from <see cref="EligibilityStartDate"/> which tracks the effective eligibility window.
    /// </summary>
    [Id(13)] public DateTime? EnrollmentDate { get; set; }
}
