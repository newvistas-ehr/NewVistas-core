// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Enrollment status codes — VistA File #27.15 DG ENROLLMENT STATUS ─────────

/// <summary>Patient enrollment status (File #27.15).</summary>
[GenerateSerializer]
public enum EnrollmentStatus
{
    /// <summary>Application received, not yet reviewed.</summary>
    Unverified = 0,

    /// <summary>Enrollment verified and active.</summary>
    Verified = 1,

    /// <summary>Previously enrolled, now inactive.</summary>
    Inactive = 2,

    /// <summary>Application rejected.</summary>
    Rejected = 3,

    /// <summary>Application cancelled by veteran.</summary>
    Cancelled = 4,

    /// <summary>Enrollment closed administratively.</summary>
    Closed = 5,

    /// <summary>Pending reapplication.</summary>
    PendingReapplication = 6,

    /// <summary>Pending — means test refusal.</summary>
    PendingMTRefusal = 7,

    /// <summary>Pending — Purple Heart veteran determination.</summary>
    PendingPurple = 8,

    /// <summary>Pending — environmental exposure review.</summary>
    PendingEnvironmental = 9,

    /// <summary>Pending — MST treatment history review.</summary>
    PendingMST = 10,

    /// <summary>Pending — other administrative hold.</summary>
    PendingOther = 11,

    /// <summary>Not eligible — veteran refused enrollment.</summary>
    NotEligibleRefused = 12,

    /// <summary>Not eligible — covered by other federal health plan.</summary>
    NotEligibleOtherFederal = 13,

    /// <summary>Not eligible — deceased veteran.</summary>
    DeceasedVeteran = 14,

    /// <summary>Pending — financial means test in progress.</summary>
    PendingMeansTest = 15,

    /// <summary>Pending — eligibility determination incomplete.</summary>
    PendingEligibility = 16,

    /// <summary>Not eligible — income threshold exceeded.</summary>
    NotEligibleIncome = 17,

    /// <summary>Enrolled — eligible for limited benefits only.</summary>
    LimitedBenefits = 18,

    /// <summary>Pending — combat veteran review period.</summary>
    PendingCombatVeteran = 19,

    /// <summary>Pending — catastrophic disability determination.</summary>
    PendingCatastrophic = 20,

    /// <summary>Not eligible — non-veteran status confirmed.</summary>
    NotEligibleNonVeteran = 21,

    /// <summary>Pending — alien eligibility review.</summary>
    PendingAlienEligibility = 22,

    /// <summary>Suspended — enrollment held pending review.</summary>
    Suspended = 23,
}

// ─── Patient Enrollment — VistA File #27.11 PATIENT ENROLLMENT ───────────────

/// <summary>
/// Patient enrollment record controlling VA healthcare eligibility and priority group
/// assignment (VistA File #27.11 PATIENT ENROLLMENT). Managed by DG ENROLLMENT
/// MUMPS routines (DGENELA.m, DGENELB.m, DGENUPL.m).
/// </summary>
[GenerateSerializer]
public class PatientEnrollmentState
{
    /// <summary>Patient identifier (links to PatientGrain key).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Current enrollment status (.07).</summary>
    [Id(1)] public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Unverified;

    /// <summary>Date the enrollment application was received (.03).</summary>
    [Id(2)] public DateTime? ApplicationDate { get; set; }

    /// <summary>Date enrollment became effective (.04).</summary>
    [Id(3)] public DateTime? EffectiveDate { get; set; }

    /// <summary>Date enrollment was terminated (.05).</summary>
    [Id(4)] public DateTime? TerminationDate { get; set; }

    /// <summary>Enrollment priority string label (e.g., "1a", "2", "5") (.01 of sub-file).</summary>
    [Id(5)] public string EnrollmentPriority { get; set; } = string.Empty;

    /// <summary>Priority group number (1–8) used for copay determination (.08).</summary>
    [Id(6)] public string PriorityGroup { get; set; } = string.Empty;

    /// <summary>Priority subgroup designation (e.g., "a", "b", "c") (.09).</summary>
    [Id(7)] public string? PrioritySubgroup { get; set; }

    /// <summary>Source of the enrollment application (e.g., ONLINE, IN-PERSON, MAIL) (.12).</summary>
    [Id(8)] public string? EnrollmentSource { get; set; }

    /// <summary>Identifier of the facility that processed the enrollment (.10).</summary>
    [Id(9)] public string? EnrollmentSiteId { get; set; }

    /// <summary>Name of the facility that processed the enrollment.</summary>
    [Id(10)] public string? EnrollmentSiteName { get; set; }

    /// <summary>Whether a means test is required for this enrollment period (.13).</summary>
    [Id(11)] public bool MeansTestRequired { get; set; }

    /// <summary>Whether veteran is exempt from the means test (.14).</summary>
    [Id(12)] public bool MeansTestExempt { get; set; }

    /// <summary>Whether an income test is required (.15).</summary>
    [Id(13)] public bool IncomeTestRequired { get; set; }

    /// <summary>Whether veteran is exempt from copay charges (.16).</summary>
    [Id(14)] public bool CopayExempt { get; set; }

    /// <summary>Reason code for copay exemption (e.g., SC, MST, CAT) (.17).</summary>
    [Id(15)] public string? CopayExemptionReason { get; set; }

    /// <summary>Date the enrollment status was last changed.</summary>
    [Id(16)] public DateTime? LastStatusChangeDate { get; set; }

    /// <summary>User ID who last changed the enrollment status.</summary>
    [Id(17)] public string? LastStatusChangedByUserId { get; set; }

    /// <summary>Free-text notes about this enrollment record.</summary>
    [Id(18)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(19)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(20)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
