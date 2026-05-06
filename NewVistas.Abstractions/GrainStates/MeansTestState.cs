// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single means test entry embedded in the patient grain.
/// Based on VistA ANNUAL MEANS TEST file (#408.31) and PATIENT ELIGIBILITY file.
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class MeansTestEntry
{
    [Id(0)]
    public string MeansTestId { get; set; } = string.Empty;

    /// <summary>
    /// Test Type � MEANS TEST, COPAY EXEMPTION TEST, GMT THRESHOLD
    /// </summary>
    [Id(2)]
    public string TestType { get; set; } = "MEANS TEST";

    /// <summary>
    /// Date of Test (.01)
    /// </summary>
    [Id(3)]
    public DateTime DateOfTest { get; set; }

    /// <summary>
    /// Status � REQUIRED, COMPLETED, NOT REQUIRED, PENDING, INCOMPLETE
    /// </summary>
    [Id(4)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Means Test Category � CATEGORY A, CATEGORY B, CATEGORY C, etc.
    /// (Affects copay obligations)
    /// </summary>
    [Id(5)]
    public string? Category { get; set; }

    /// <summary>
    /// Annual Income � veteran household income
    /// </summary>
    [Id(6)]
    public decimal? AnnualIncome { get; set; }

    /// <summary>
    /// Net Worth
    /// </summary>
    [Id(7)]
    public decimal? NetWorth { get; set; }

    /// <summary>
    /// Number of Dependents
    /// </summary>
    [Id(8)]
    public int? NumberOfDependents { get; set; }

    /// <summary>
    /// Income Threshold � the threshold for the year
    /// </summary>
    [Id(9)]
    public decimal? IncomeThreshold { get; set; }

    /// <summary>
    /// Copay Exempt � whether patient is exempt from copays
    /// </summary>
    [Id(10)]
    public bool IsCopayExempt { get; set; }

    /// <summary>
    /// Primary Eligibility Code
    /// </summary>
    [Id(11)]
    public string? PrimaryEligibilityCode { get; set; }

    /// <summary>
    /// Eligibility Status � VERIFIED, NOT VERIFIED
    /// </summary>
    [Id(12)]
    public string? EligibilityStatus { get; set; }

    /// <summary>
    /// Enrollment Priority Group � 1 through 8
    /// </summary>
    [Id(13)]
    public string? PriorityGroup { get; set; }

    /// <summary>
    /// Enrollment Status � VERIFIED, PENDING, NOT ENROLLED, REJECTED
    /// </summary>
    [Id(14)]
    public string? EnrollmentStatus { get; set; }

    /// <summary>
    /// Enrollment Date
    /// </summary>
    [Id(15)]
    public DateTime? EnrollmentDate { get; set; }

    /// <summary>
    /// Completed By
    /// </summary>
    [Id(16)]
    public string? CompletedById { get; set; }

    [Id(17)]
    public string? CompletedByName { get; set; }

    [Id(18)]
    public string? Comments { get; set; }

    [Id(19)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(20)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Veteran Gross Income
    /// </summary>
    [Id(21)]
    public decimal? VeteranGrossIncome { get; set; }

    /// <summary>
    /// Spouse Gross Income
    /// </summary>
    [Id(22)]
    public decimal? SpouseGrossIncome { get; set; }

    /// <summary>
    /// Dependent Income
    /// </summary>
    [Id(23)]
    public decimal? DependentIncome { get; set; }

    /// <summary>
    /// Total Net Worth — assets
    /// </summary>
    [Id(24)]
    public decimal? TotalNetWorth { get; set; }

    /// <summary>
    /// Deductible Expenses
    /// </summary>
    [Id(25)]
    public decimal? DeductibleExpenses { get; set; }

    /// <summary>
    /// Adjusted Income — computed (income minus deductible expenses)
    /// </summary>
    [Id(26)]
    public decimal? AdjustedIncome { get; set; }

    /// <summary>
    /// Geographic Means Test threshold
    /// </summary>
    [Id(27)]
    public decimal? GmtThreshold { get; set; }

    /// <summary>
    /// Hardship Determination — "HARDSHIP", "NOT_HARDSHIP"
    /// </summary>
    [Id(28)]
    public string? HardshipDetermination { get; set; }

    /// <summary>
    /// Hardship Decision Date
    /// </summary>
    [Id(29)]
    public DateTime? HardshipDecisionDate { get; set; }

    /// <summary>
    /// Copay Test Result — "EXEMPT", "FIRST_PARTY", "COPAY_REQUIRED"
    /// </summary>
    [Id(30)]
    public string? CopayTestResult { get; set; }

    /// <summary>
    /// Link to prior year means test
    /// </summary>
    [Id(31)]
    public string? PreviousYearTestId { get; set; }

    /// <summary>
    /// Property Value
    /// </summary>
    [Id(32)]
    public decimal? PropertyValue { get; set; }

    /// <summary>
    /// Other Assets
    /// </summary>
    [Id(33)]
    public decimal? OtherAssets { get; set; }

    /// <summary>
    /// Dependent details
    /// </summary>
    [Id(34)]
    public List<MeansTestDependent> Dependents { get; set; } = new();
}

/// <summary>
/// Dependent details for means test
/// </summary>
[GenerateSerializer]
public class MeansTestDependent
{
    /// <summary>
    /// Dependent name
    /// </summary>
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Relationship — "SPOUSE", "CHILD", "DEPENDENT_PARENT"
    /// </summary>
    [Id(1)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>
    /// Dependent income
    /// </summary>
    [Id(2)]
    public decimal Income { get; set; }

    /// <summary>
    /// Dependent net worth
    /// </summary>
    [Id(3)]
    public decimal NetWorth { get; set; }

    /// <summary>
    /// Dependent date of birth
    /// </summary>
    [Id(4)]
    public DateTime? DateOfBirth { get; set; }
}
