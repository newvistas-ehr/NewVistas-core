// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Group Insurance Plan record (VistA File #355.3 GROUP INSURANCE PLAN).
/// Represents a shared insurance plan definition — Medicare, Medicaid, CHAMPVA, TRICARE,
/// or a private group plan — that can be referenced by individual patient personal policies.
/// Grain key: "IB-PLAN:{planId}"
/// </summary>
[GenerateSerializer]
public class InsurancePlanState
{
    /// <summary>Unique plan identifier (GUID string). (.001)</summary>
    [Id(0)] public string PlanId { get; set; } = string.Empty;

    /// <summary>Name of the group insurance plan. (.01)</summary>
    [Id(1)] public string GroupPlanName { get; set; } = string.Empty;

    /// <summary>Name of the insurance company offering this plan. (.02)</summary>
    [Id(2)] public string InsuranceCompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Type of plan from File #355.1 TYPE OF PLAN.
    /// Common values: MEDICARE, MEDICAID, CHAMPVA, TRICARE, COMMERCIAL, SUPPLEMENTAL.
    /// </summary>
    [Id(3)] public string? PlanType { get; set; }

    /// <summary>Group plan number assigned by the insurance company. (.03)</summary>
    [Id(4)] public string? GroupNumber { get; set; }

    /// <summary>
    /// Type of insurance coverage from File #355.2 TYPE OF INSURANCE COVERAGE.
    /// Examples: INDIVIDUAL, FAMILY, EMPLOYEE+SPOUSE.
    /// </summary>
    [Id(5)] public string? CoverageType { get; set; }

    /// <summary>Date this plan's coverage began. (.04)</summary>
    [Id(6)] public DateTime? EffectiveDate { get; set; }

    /// <summary>Date this plan's coverage ends. Null = no expiration. (.05)</summary>
    [Id(7)] public DateTime? ExpirationDate { get; set; }

    /// <summary>Whether this plan is currently active and accepting new member enrollments. (.06)</summary>
    [Id(8)] public bool IsActive { get; set; } = true;

    /// <summary>Date and time insurance eligibility was last verified. (.07)</summary>
    [Id(9)] public DateTime? VerificationDateTime { get; set; }

    /// <summary>
    /// Source of the last verification (File #355.12 SOURCE OF INFORMATION).
    /// Examples: PHONE, ELECTRONIC, CARD, LETTER.
    /// </summary>
    [Id(10)] public string? VerificationSource { get; set; }

    /// <summary>Whether this insurer supports electronic eligibility verification (270/271 EDI). (.08)</summary>
    [Id(11)] public bool AllowsElectronicVerification { get; set; }

    /// <summary>Whether pre-certification is required before services are rendered. (.09)</summary>
    [Id(12)] public bool IsPreCertRequired { get; set; }

    /// <summary>
    /// Number of days after service in which a claim must be filed (File #355.13).
    /// Null = no stated limit.
    /// </summary>
    [Id(13)] public int? FilingTimeFrameDays { get; set; }

    /// <summary>Patient's coinsurance percentage (0–100). Null = not applicable. (.10)</summary>
    [Id(14)] public decimal? CoinsurancePercent { get; set; }

    /// <summary>Annual deductible amount. Null = not applicable or unknown. (.11)</summary>
    [Id(15)] public decimal? DeductibleAmount { get; set; }

    /// <summary>Maximum annual benefit payable by this plan. Null = unlimited/unknown. (.12)</summary>
    [Id(16)] public decimal? AnnualMaxBenefit { get; set; }

    /// <summary>Address where paper claims should be mailed. (.13)</summary>
    [Id(17)] public string? ClaimsAddress { get; set; }

    /// <summary>Phone number for claims inquiries. (.14)</summary>
    [Id(18)] public string? ClaimsPhone { get; set; }

    /// <summary>Pharmacy BIN (Bank Identification Number) for PBM routing. (.15)</summary>
    [Id(19)] public string? PharmacyBinNumber { get; set; }

    /// <summary>Pharmacy PCN (Processor Control Number) for PBM routing. (.16)</summary>
    [Id(20)] public string? PharmacyPcnNumber { get; set; }

    /// <summary>Free-text notes about this plan.</summary>
    [Id(21)] public string? Notes { get; set; }

    /// <summary>Date this grain record was first created.</summary>
    [Id(22)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this grain record was last modified.</summary>
    [Id(23)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
