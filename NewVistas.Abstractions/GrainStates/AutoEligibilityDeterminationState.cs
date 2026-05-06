// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Result of an automatic eligibility determination.
/// </summary>
[GenerateSerializer]
public enum EligibilityDeterminationResult
{
    /// <summary>Not yet determined.</summary>
    Pending = 0,
    /// <summary>Eligible — no copay required.</summary>
    Exempt = 1,
    /// <summary>Eligible with first-party copay obligation.</summary>
    CopayRequired = 2,
    /// <summary>Eligible — hardship exemption granted.</summary>
    HardshipExempt = 3,
    /// <summary>Not eligible based on income/means test.</summary>
    NotEligibleIncome = 4,
    /// <summary>Not eligible — enrollment rejected or closed.</summary>
    NotEligible = 5,
}

/// <summary>
/// State for an automatic eligibility determination record.
/// Integrates means test results, enrollment status, priority group, and income thresholds
/// to automatically determine copay obligation and enrollment eligibility.
/// Maps to VistA DG/IB integration (DGENELA.m, IBCNEDE.m).
/// Grain key: "ELIG-DET:{patientId}"
/// </summary>
[GenerateSerializer]
public class AutoEligibilityDeterminationState
{
    /// <summary>Determination ID — grain key.</summary>
    [Id(0)] public string DeterminationId { get; set; } = string.Empty;

    /// <summary>Patient being evaluated.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Date the determination was computed.</summary>
    [Id(2)] public DateTime DeterminationDate { get; set; }

    /// <summary>Final determination result.</summary>
    [Id(3)] public EligibilityDeterminationResult Result { get; set; }

    // ─── Input factors ───────────────────────────────────────────────────

    /// <summary>Enrollment status at time of determination.</summary>
    [Id(4)] public string EnrollmentStatus { get; set; } = string.Empty;

    /// <summary>Priority group at time of determination.</summary>
    [Id(5)] public string? PriorityGroup { get; set; }

    /// <summary>Priority subgroup.</summary>
    [Id(6)] public string? PrioritySubgroup { get; set; }

    /// <summary>Whether a means test was required.</summary>
    [Id(7)] public bool MeansTestRequired { get; set; }

    /// <summary>Whether means test was completed.</summary>
    [Id(8)] public bool MeansTestCompleted { get; set; }

    /// <summary>Means test ID used (if applicable).</summary>
    [Id(9)] public string? MeansTestId { get; set; }

    /// <summary>Adjusted income from means test.</summary>
    [Id(10)] public decimal? AdjustedIncome { get; set; }

    /// <summary>Geographic Means Test threshold for comparison.</summary>
    [Id(11)] public decimal? GmtThreshold { get; set; }

    /// <summary>Copay test result from means test (EXEMPT, FIRST_PARTY, COPAY_REQUIRED).</summary>
    [Id(12)] public string? CopayTestResult { get; set; }

    /// <summary>Whether patient has service-connected disability >= 50%.</summary>
    [Id(13)] public bool IsServiceConnected50Plus { get; set; }

    /// <summary>Service-connected disability percentage.</summary>
    [Id(14)] public int? ServiceConnectedPercent { get; set; }

    /// <summary>Whether patient receives VA pension.</summary>
    [Id(15)] public bool ReceivesVaPension { get; set; }

    /// <summary>Whether patient is catastrophically disabled.</summary>
    [Id(16)] public bool IsCatastrophicallyDisabled { get; set; }

    /// <summary>Whether patient is a former POW.</summary>
    [Id(17)] public bool IsFormerPOW { get; set; }

    /// <summary>Whether patient received Purple Heart.</summary>
    [Id(18)] public bool IsPurpleHeart { get; set; }

    // ─── Output ──────────────────────────────────────────────────────────

    /// <summary>Whether copay is required.</summary>
    [Id(19)] public bool CopayRequired { get; set; }

    /// <summary>Copay exemption reason (if exempt).</summary>
    [Id(20)] public string? CopayExemptionReason { get; set; }

    /// <summary>Assigned priority group based on determination.</summary>
    [Id(21)] public string? AssignedPriorityGroup { get; set; }

    /// <summary>Recommended enrollment status change (if any).</summary>
    [Id(22)] public string? RecommendedEnrollmentStatus { get; set; }

    /// <summary>Reasons/factors that contributed to the determination.</summary>
    [Id(23)] public List<string> DeterminationFactors { get; set; } = new();

    /// <summary>User/system that triggered the determination.</summary>
    [Id(24)] public string? DeterminedByUserId { get; set; }

    /// <summary>Display name.</summary>
    [Id(25)] public string? DeterminedByUserName { get; set; }

    /// <summary>Whether the determination was auto-applied to enrollment.</summary>
    [Id(26)] public bool AutoApplied { get; set; }

    /// <summary>Optional notes.</summary>
    [Id(27)] public string? Notes { get; set; }

    /// <summary>When created.</summary>
    [Id(28)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(29)] public DateTime LastModifiedDate { get; set; }
}
