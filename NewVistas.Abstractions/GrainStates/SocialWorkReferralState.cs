// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Type of community or VA service the patient is being referred to.
/// </summary>
[GenerateSerializer]
public enum SocialWorkReferralServiceType
{
    Housing = 0,
    Transportation = 1,
    Food = 2,
    CommunityMentalHealth = 3,
    SubstanceAbuseTreatment = 4,
    VocationalRehabilitation = 5,
    FinancialAssistance = 6,
    LegalServices = 7,
    HomeHealth = 8,
    HomelessServices = 9,
    Caregiver = 10,
    AdultProtectiveServices = 11,
    Other = 12,
}

/// <summary>
/// Lifecycle status of a social work referral.
/// </summary>
[GenerateSerializer]
public enum SocialWorkReferralStatus
{
    Pending = 0,
    Active = 1,
    Complete = 2,
    Declined = 3,
    FollowUpNeeded = 4,
    Closed = 5,
}

/// <summary>
/// Persistent state for a Social Work Referral grain.
/// VistA File #707 — SOCIAL WORK ASSESSMENT (referral sub-file).
/// Mirrors SWR*.m referral tracking routines.
/// </summary>
[GenerateSerializer]
public class SocialWorkReferralState
{
    /// <summary>
    /// Unique grain key (SW-REFERRAL:{guid}).
    /// </summary>
    [Id(0)]
    public string ReferralId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (.01).
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Date/time referral was placed (.02).
    /// </summary>
    [Id(2)]
    public DateTime ReferralDate { get; set; }

    /// <summary>
    /// Referral source — who initiated the referral (free text).
    /// </summary>
    [Id(3)]
    public string? ReferralSource { get; set; }

    /// <summary>
    /// Clinical reason for the referral.
    /// </summary>
    [Id(4)]
    public string? ReferralReason { get; set; }

    /// <summary>
    /// Category of service being requested (.03).
    /// </summary>
    [Id(5)]
    public SocialWorkReferralServiceType ServiceType { get; set; }

    /// <summary>
    /// Name of receiving agency or program.
    /// </summary>
    [Id(6)]
    public string? AgencyName { get; set; }

    /// <summary>
    /// Primary contact at the receiving agency.
    /// </summary>
    [Id(7)]
    public string? AgencyContact { get; set; }

    /// <summary>
    /// Phone number for the receiving agency.
    /// </summary>
    [Id(8)]
    public string? AgencyPhone { get; set; }

    /// <summary>
    /// Lifecycle status (.04).
    /// </summary>
    [Id(9)]
    public SocialWorkReferralStatus Status { get; set; }

    /// <summary>
    /// Social worker DUZ who placed the referral.
    /// </summary>
    [Id(10)]
    public string? SocialWorkerId { get; set; }

    [Id(11)]
    public string? SocialWorkerName { get; set; }

    /// <summary>
    /// Date/time the referral was accepted/activated by the receiving agency.
    /// </summary>
    [Id(12)]
    public DateTime? AcceptedDate { get; set; }

    /// <summary>
    /// Date/time the referral was completed or closed.
    /// </summary>
    [Id(13)]
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// Outcome narrative — what services were provided, why declined, etc.
    /// </summary>
    [Id(14)]
    public string? OutcomeNotes { get; set; }

    /// <summary>
    /// Date the next follow-up contact is due.
    /// </summary>
    [Id(15)]
    public DateTime? FollowUpDate { get; set; }

    /// <summary>
    /// Linked assessment ID (if referral stems from a formal assessment).
    /// </summary>
    [Id(16)]
    public string? AssessmentId { get; set; }

    /// <summary>
    /// Location/clinic where the referral was placed.
    /// </summary>
    [Id(17)]
    public string? LocationId { get; set; }

    [Id(18)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Additional free-text comments.
    /// </summary>
    [Id(19)]
    public string? Comments { get; set; }

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry stored in the per-patient referral index.
/// </summary>
[GenerateSerializer]
public class SocialWorkReferralIndexEntry
{
    [Id(0)]
    public string ReferralId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public DateTime ReferralDate { get; set; }

    [Id(3)]
    public SocialWorkReferralServiceType ServiceType { get; set; }

    [Id(4)]
    public string? AgencyName { get; set; }

    [Id(5)]
    public SocialWorkReferralStatus Status { get; set; }

    [Id(6)]
    public string? SocialWorkerName { get; set; }

    [Id(7)]
    public DateTime? FollowUpDate { get; set; }
}

/// <summary>
/// State class for the per-patient referral index grain (SW-REFERRAL-IDX:{patientId}).
/// </summary>
[GenerateSerializer]
public class SocialWorkReferralIndexState
{
    /// <summary>
    /// Ordered list of referral summaries (newest first).
    /// </summary>
    [Id(0)]
    public List<SocialWorkReferralIndexEntry> Entries { get; set; } = new();
}
