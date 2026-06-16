// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Personal insurance policy record (VistA File #355.7 PERSONAL POLICY).
/// Represents a specific patient's enrollment in a group insurance plan,
/// including subscriber details, relationship, coverage dates, and COBRA status.
/// Grain key: "IB-POLICY:{policyId}"
/// </summary>
[GenerateSerializer]
public class PersonalPolicyState
{
    /// <summary>Unique policy identifier (GUID string). (.001)</summary>
    [Id(0)] public string PolicyId { get; set; } = string.Empty;

    /// <summary>Patient DFN/identifier this policy belongs to. (.01)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the group plan grain this policy is enrolled in.
    /// Links to InsurancePlanGrain (File #355.3). Null if plan not in system.
    /// </summary>
    [Id(2)] public string? GroupPlanId { get; set; }

    /// <summary>Display name of the group plan (denormalized for quick display). (.02)</summary>
    [Id(3)] public string GroupPlanName { get; set; } = string.Empty;

    /// <summary>Member/subscriber ID assigned by the insurance company. (.03)</summary>
    [Id(4)] public string SubscriberId { get; set; } = string.Empty;

    /// <summary>Name of the primary subscriber (may differ from patient for dependent coverage). (.04)</summary>
    [Id(5)] public string? SubscriberName { get; set; }

    /// <summary>
    /// Patient's relationship to the subscriber (File #355.81 SPONSOR RELATIONSHIP).
    /// Common values: SELF, SPOUSE, CHILD, PARENT, OTHER.
    /// </summary>
    [Id(6)] public string? RelationshipToSubscriber { get; set; }

    /// <summary>Date this policy's coverage became effective. (.05)</summary>
    [Id(7)] public DateTime? EffectiveDate { get; set; }

    /// <summary>Date this policy's coverage expires. Null = no expiration. (.06)</summary>
    [Id(8)] public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Type of coverage for this policy (File #355.2 TYPE OF INSURANCE COVERAGE).
    /// Examples: INDIVIDUAL, FAMILY, EMPLOYEE+SPOUSE.
    /// </summary>
    [Id(9)] public string? CoverageType { get; set; }

    /// <summary>Whether this is the patient's primary insurance policy. (.07)</summary>
    [Id(10)] public bool IsPrimary { get; set; }

    /// <summary>Whether this policy is currently active. (.08)</summary>
    [Id(11)] public bool IsActive { get; set; } = true;

    /// <summary>Whether this policy is under COBRA continuation coverage. (.09)</summary>
    [Id(12)] public bool CobraFlag { get; set; }

    /// <summary>COBRA coverage start date. (.10)</summary>
    [Id(13)] public DateTime? CobraStartDate { get; set; }

    /// <summary>COBRA coverage end date. Null = COBRA period ongoing. (.11)</summary>
    [Id(14)] public DateTime? CobraEndDate { get; set; }

    /// <summary>Patient's copay amount under this policy (may override plan default). (.12)</summary>
    [Id(15)] public decimal? CopayAmount { get; set; }

    /// <summary>Whether the annual deductible has been met for the current benefit year. (.13)</summary>
    [Id(16)] public bool DeductibleMet { get; set; }

    /// <summary>Date the deductible was met for the current benefit year.</summary>
    [Id(17)] public DateTime? DeductibleMetDate { get; set; }

    /// <summary>Year-to-date benefit amount used against this policy (dollar amount paid by insurer). (.14)</summary>
    [Id(18)] public decimal YearToDateBenefitsUsed { get; set; }

    /// <summary>Patient's pharmacy member ID if different from medical subscriber ID. (.15)</summary>
    [Id(19)] public string? PharmacyMemberId { get; set; }

    /// <summary>Free-text notes about this policy.</summary>
    [Id(20)] public string? Notes { get; set; }

    /// <summary>Date this grain record was first created.</summary>
    [Id(21)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this grain record was last modified.</summary>
    [Id(22)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
