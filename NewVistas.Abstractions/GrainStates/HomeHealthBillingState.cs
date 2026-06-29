// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Status of the Medicare Notice of Admission (NOA).</summary>
public enum NoaStatus
{
    NotSubmitted,
    Submitted,
    Accepted,
    Rejected
}

/// <summary>Status of a Medicare home-health claim (one per 30-day payment period).</summary>
public enum HomeHealthClaimStatus
{
    Draft,
    Submitted,
    Paid,
    Denied
}

/// <summary>
/// The Notice of Admission — submitted once per episode within 5 days of Start of Care (it
/// replaced the RAP). Late submission incurs a per-day payment reduction.
/// </summary>
[GenerateSerializer]
public class NoticeOfAdmission
{
    [Id(0)] public NoaStatus Status { get; set; } = NoaStatus.NotSubmitted;
    [Id(1)] public DateTime AdmissionDate { get; set; }
    [Id(2)] public DateTime? SubmittedDate { get; set; }
    [Id(3)] public DateTime? AcceptedDate { get; set; }
    [Id(4)] public string ControlNumber { get; set; } = string.Empty;
    /// <summary>True when submitted more than 5 days after Start of Care.</summary>
    [Id(5)] public bool IsLate { get; set; }
}

/// <summary>A Medicare home-health claim for one 30-day payment period.</summary>
[GenerateSerializer]
public class HomeHealthClaim
{
    [Id(0)] public string ClaimId { get; set; } = string.Empty;
    [Id(1)] public string CertificationPeriodId { get; set; } = string.Empty;
    [Id(2)] public string PaymentPeriodId { get; set; } = string.Empty;
    /// <summary>The PDGM HIPPS case-mix code billed.</summary>
    [Id(3)] public string HippsCode { get; set; } = string.Empty;
    /// <summary>True when paid per-visit under LUPA rather than the full 30-day rate.</summary>
    [Id(4)] public bool IsLupa { get; set; }
    [Id(5)] public HomeHealthClaimStatus Status { get; set; } = HomeHealthClaimStatus.Draft;
    [Id(6)] public DateTime? SubmittedDate { get; set; }
    [Id(7)] public string ControlNumber { get; set; } = string.Empty;
    [Id(8)] public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Medicare home-health billing for one episode — the Notice of Admission and the per-period
/// claims. Key pattern: "HHC-BILLING:{episodeId}". (Phase 2 / HOME_HEALTH_MEDICARE.)
/// </summary>
[GenerateSerializer]
public class HomeHealthBillingState
{
    [Id(0)] public string EpisodeId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public NoticeOfAdmission Noa { get; set; } = new();
    [Id(3)] public List<HomeHealthClaim> Claims { get; set; } = new();
    [Id(4)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
