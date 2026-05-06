// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainStates;

/// <summary>
/// State for a physical therapy referral from an external referring provider.
/// Represents the "piece of paper" — diagnosis, treatment intent, and visit authorization.
/// </summary>
[GenerateSerializer]
public class PTReferralState
{
    /// <summary>Grain key, set on activation.</summary>
    [Id(0)] public string ReferralId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient display name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    // ── Referring Provider ─────────────────────────────────

    /// <summary>Referring provider's name (external, typically not a system user).</summary>
    [Id(3)] public string? ReferringProviderName { get; set; }

    /// <summary>Referring provider's external identifier (e.g., NPI).</summary>
    [Id(4)] public string? ReferringProviderId { get; set; }

    /// <summary>Referring provider's specialty (e.g., "Orthopedics", "Primary Care").</summary>
    [Id(5)] public string? ReferringProviderSpecialty { get; set; }

    /// <summary>Referring provider's facility or practice name.</summary>
    [Id(6)] public string? ReferringFacilityName { get; set; }

    // ── Clinical Context ("the piece of paper") ───────────

    /// <summary>Diagnosis text (e.g., "L shoulder rotator cuff tendinopathy").</summary>
    [Id(7)] public string? Diagnosis { get; set; }

    /// <summary>ICD-10 diagnosis code.</summary>
    [Id(8)] public string? DiagnosisCode { get; set; }

    /// <summary>Targeted body groups for this referral.</summary>
    [Id(9)] public List<BodyGroup> BodyGroups { get; set; } = new();

    /// <summary>Reason for referral — what the provider wants addressed.</summary>
    [Id(10)] public string? ReasonForReferral { get; set; }

    /// <summary>Precautions or contraindications (e.g., "no overhead > 90 deg post-surgery").</summary>
    [Id(11)] public string? Precautions { get; set; }

    // ── Visit Authorization ───────────────────────────────

    /// <summary>Number of authorized visits. 0 = unlimited / not specified.</summary>
    [Id(12)] public int AuthorizedVisits { get; set; }

    /// <summary>Number of visits consumed against this referral.</summary>
    [Id(13)] public int UsedVisits { get; set; }

    /// <summary>Authorization expiration date (e.g., insurance limit).</summary>
    [Id(14)] public DateTime? AuthorizationExpirationDate { get; set; }

    // ── Lifecycle ─────────────────────────────────────────

    /// <summary>Current referral status.</summary>
    [Id(15)] public PTReferralStatus Status { get; set; } = PTReferralStatus.Active;

    /// <summary>Date on the referral document.</summary>
    [Id(16)] public DateTime ReferralDate { get; set; }

    /// <summary>Date the PT clinic received the referral.</summary>
    [Id(17)] public DateTime? ReceivedDate { get; set; }

    /// <summary>Free-text notes about this referral.</summary>
    [Id(18)] public string? Notes { get; set; }

    // ── Timestamps ────────���───────────────────────────────

    /// <summary>When the grain was first created.</summary>
    [Id(19)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modification timestamp.</summary>
    [Id(20)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight index entry for a PT referral, stored in the per-patient index grain.
/// </summary>
[GenerateSerializer]
public class PTReferralIndexEntry
{
    /// <summary>The referral grain key (e.g., "PTREF:PAT001:abc-def-123").</summary>
    [Id(0)] public string ReferralGrainKey { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Referring provider name, for display.</summary>
    [Id(2)] public string? ReferringProviderName { get; set; }

    /// <summary>Diagnosis text, for display.</summary>
    [Id(3)] public string? Diagnosis { get; set; }

    /// <summary>Authorized visit count.</summary>
    [Id(4)] public int AuthorizedVisits { get; set; }

    /// <summary>Visits consumed so far.</summary>
    [Id(5)] public int UsedVisits { get; set; }

    /// <summary>Current referral status.</summary>
    [Id(6)] public PTReferralStatus Status { get; set; }

    /// <summary>Date on the referral document.</summary>
    [Id(7)] public DateTime ReferralDate { get; set; }

    /// <summary>Authorization expiration date.</summary>
    [Id(8)] public DateTime? AuthorizationExpirationDate { get; set; }
}

/// <summary>
/// State for the per-patient PT referral index grain.
/// Maintains all referral entries for one patient.
/// </summary>
[GenerateSerializer]
public class PTReferralIndexState
{
    /// <summary>Patient identifier (from grain key).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All referral index entries for this patient.</summary>
    [Id(1)] public List<PTReferralIndexEntry> Entries { get; set; } = new();

    /// <summary>Last modification timestamp.</summary>
    [Id(2)] public DateTime LastModifiedDate { get; set; }
}
