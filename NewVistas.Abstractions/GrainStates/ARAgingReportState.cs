// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Aging bucket classification for AR accounts.
/// Standard healthcare AR aging buckets per PRCA reporting.
/// </summary>
[GenerateSerializer]
public enum AgingBucket
{
    /// <summary>0-30 days since charge establishment.</summary>
    Current = 0,

    /// <summary>31-60 days past due.</summary>
    ThirtyOneToSixty = 1,

    /// <summary>61-90 days past due.</summary>
    SixtyOneToNinety = 2,

    /// <summary>91-120 days past due.</summary>
    NinetyOneToOneTwenty = 3,

    /// <summary>121+ days past due.</summary>
    OverOneTwenty = 4,
}

/// <summary>
/// Summary of a single aging bucket in a report.
/// </summary>
[GenerateSerializer]
public record AgingBucketSummary
{
    /// <summary>Which aging bucket.</summary>
    [Id(0)] public AgingBucket Bucket { get; init; }

    /// <summary>Human-readable label (e.g., "0-30 Days").</summary>
    [Id(1)] public string Label { get; init; } = string.Empty;

    /// <summary>Number of accounts in this bucket.</summary>
    [Id(2)] public int AccountCount { get; init; }

    /// <summary>Total outstanding balance in this bucket.</summary>
    [Id(3)] public decimal TotalBalance { get; init; }

    /// <summary>Percentage of total AR in this bucket (0.0–1.0).</summary>
    [Id(4)] public decimal PercentOfTotal { get; init; }
}

/// <summary>
/// An individual account entry within an aging report.
/// </summary>
[GenerateSerializer]
public record AgingAccountDetail
{
    /// <summary>AR account ID.</summary>
    [Id(0)] public string ARAccountId { get; init; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Account category.</summary>
    [Id(2)] public ARAccountCategory Category { get; init; }

    /// <summary>Account status.</summary>
    [Id(3)] public ARAccountStatus Status { get; init; }

    /// <summary>Original charge amount.</summary>
    [Id(4)] public decimal OriginalAmount { get; init; }

    /// <summary>Current outstanding balance.</summary>
    [Id(5)] public decimal CurrentBalance { get; init; }

    /// <summary>Date the account was established.</summary>
    [Id(6)] public DateTime DateEstablished { get; init; }

    /// <summary>Number of days since account was established.</summary>
    [Id(7)] public int DaysOutstanding { get; init; }

    /// <summary>Which aging bucket this account falls into.</summary>
    [Id(8)] public AgingBucket Bucket { get; init; }
}

/// <summary>
/// Revenue cycle metrics for dashboard display.
/// </summary>
[GenerateSerializer]
public record RevenueCycleMetrics
{
    /// <summary>Total AR balance across all active accounts.</summary>
    [Id(0)] public decimal TotalARBalance { get; init; }

    /// <summary>Total number of active AR accounts.</summary>
    [Id(1)] public int TotalActiveAccounts { get; init; }

    /// <summary>Average days outstanding for all active accounts.</summary>
    [Id(2)] public decimal AverageDaysOutstanding { get; init; }

    /// <summary>Collection rate = TotalPaid / (TotalPaid + TotalBalance).</summary>
    [Id(3)] public decimal CollectionRate { get; init; }

    /// <summary>Total payments received in the reporting period.</summary>
    [Id(4)] public decimal TotalPaymentsReceived { get; init; }

    /// <summary>Total new charges in the reporting period.</summary>
    [Id(5)] public decimal TotalNewCharges { get; init; }

    /// <summary>Number of accounts currently delinquent.</summary>
    [Id(6)] public int DelinquentAccountCount { get; init; }

    /// <summary>Total balance of delinquent accounts.</summary>
    [Id(7)] public decimal DelinquentBalance { get; init; }

    /// <summary>Number of accounts referred to collection.</summary>
    [Id(8)] public int InCollectionCount { get; init; }

    /// <summary>Total balance referred to collection.</summary>
    [Id(9)] public decimal InCollectionBalance { get; init; }

    /// <summary>Number of accounts referred to Treasury Offset Program.</summary>
    [Id(10)] public int TopReferralCount { get; init; }

    /// <summary>When this report was generated.</summary>
    [Id(11)] public DateTime ReportDate { get; init; }
}

/// <summary>
/// State for an AR aging report snapshot.
/// Captures a point-in-time view of all patient AR accounts for financial reporting.
/// MUMPS: PRCA AR aging reports, RCRP*.m
/// Grain key: "AR-AGING:{patientId}" (per-patient) or "AR-AGING-FACILITY:{date}" (facility-wide)
/// </summary>
[GenerateSerializer]
public class ARAgingReportState
{
    /// <summary>Report identifier — grain key.</summary>
    [Id(0)] public string ReportId { get; set; } = string.Empty;

    /// <summary>Patient ID (for per-patient reports; empty for facility-wide).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Date the report was generated.</summary>
    [Id(2)] public DateTime ReportDate { get; set; }

    /// <summary>Aging bucket summaries.</summary>
    [Id(3)] public List<AgingBucketSummary> BucketSummaries { get; set; } = new();

    /// <summary>Individual account details within the report.</summary>
    [Id(4)] public List<AgingAccountDetail> AccountDetails { get; set; } = new();

    /// <summary>Revenue cycle metrics summary.</summary>
    [Id(5)] public RevenueCycleMetrics? Metrics { get; set; }

    /// <summary>Total AR balance at time of report.</summary>
    [Id(6)] public decimal TotalBalance { get; set; }

    /// <summary>Total number of accounts in the report.</summary>
    [Id(7)] public int TotalAccounts { get; set; }

    /// <summary>User who generated the report.</summary>
    [Id(8)] public string? GeneratedByUserId { get; set; }

    /// <summary>Display name of generating user.</summary>
    [Id(9)] public string? GeneratedByUserName { get; set; }

    /// <summary>When this record was created.</summary>
    [Id(10)] public DateTime CreatedDate { get; set; }

    /// <summary>When this record was last modified.</summary>
    [Id(11)] public DateTime LastModifiedDate { get; set; }
}
