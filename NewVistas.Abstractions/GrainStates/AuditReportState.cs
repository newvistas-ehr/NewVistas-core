// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a generated audit report.
/// §170.315(d)(3) — Audit Report(s): on-demand formal report with filtering,
/// aggregation statistics, and hash-chain integrity verification.
///
/// Grain Key: "AUDIT-REPORT:{reportId}"
/// </summary>
[GenerateSerializer]
public class AuditReportState
{
    /// <summary>Unique report identifier.</summary>
    [Id(0)]
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Human-readable title (e.g., "Patient Audit Report — 2026-01 to 2026-03").</summary>
    [Id(1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Report type: "patient", "user", "system".</summary>
    [Id(2)]
    public string ReportType { get; set; } = string.Empty;

    /// <summary>Patient ID if this is a patient-scoped report.</summary>
    [Id(3)]
    public string? PatientId { get; set; }

    /// <summary>User ID filter (if report is scoped to a specific user's actions).</summary>
    [Id(4)]
    public string? UserId { get; set; }

    /// <summary>Domain filter (e.g., "ORDERS", "PHARMACY").</summary>
    [Id(5)]
    public string? DomainFilter { get; set; }

    /// <summary>Action filter (e.g., "UPDATE", "DELETE").</summary>
    [Id(6)]
    public string? ActionFilter { get; set; }

    /// <summary>Start of the reporting period.</summary>
    [Id(7)]
    public DateTime PeriodStart { get; set; }

    /// <summary>End of the reporting period.</summary>
    [Id(8)]
    public DateTime PeriodEnd { get; set; }

    /// <summary>When the report was generated.</summary>
    [Id(9)]
    public DateTime GeneratedDate { get; set; }

    /// <summary>Who requested the report.</summary>
    [Id(10)]
    public string? GeneratedBy { get; set; }

    // ─── Results ──────────────────────────────────────────────────────────────

    /// <summary>Total number of audit events in the report.</summary>
    [Id(11)]
    public int TotalEvents { get; set; }

    /// <summary>Count of events by domain (e.g., ORDERS: 42, LABS: 15).</summary>
    [Id(12)]
    public Dictionary<string, int> EventsByDomain { get; set; } = new();

    /// <summary>Count of events by action type (e.g., VIEW: 120, UPDATE: 30).</summary>
    [Id(13)]
    public Dictionary<string, int> EventsByAction { get; set; } = new();

    /// <summary>Count of events by user (e.g., "SMITH,JOHN": 45).</summary>
    [Id(14)]
    public Dictionary<string, int> EventsByUser { get; set; } = new();

    /// <summary>The audit events included in this report.</summary>
    [Id(15)]
    public List<AuditEventSummary> Events { get; set; } = new();

    // ─── Integrity ────────────────────────────────────────────────────────────

    /// <summary>Number of events that passed hash-chain integrity verification.</summary>
    [Id(16)]
    public int IntegrityPassCount { get; set; }

    /// <summary>Number of events that failed hash-chain integrity verification.</summary>
    [Id(17)]
    public int IntegrityFailCount { get; set; }

    /// <summary>Event IDs that failed integrity verification (tamper-detected).</summary>
    [Id(18)]
    public List<string> IntegrityFailures { get; set; } = new();

    /// <summary>Overall integrity status: "verified", "tamper-detected", "not-checked".</summary>
    [Id(19)]
    public string IntegrityStatus { get; set; } = "not-checked";

    [Id(20)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry for the audit report index.
/// </summary>
[GenerateSerializer]
public class AuditReportSummary
{
    [Id(0)]
    public string ReportId { get; set; } = string.Empty;

    [Id(1)]
    public string Title { get; set; } = string.Empty;

    [Id(2)]
    public string ReportType { get; set; } = string.Empty;

    [Id(3)]
    public string? PatientId { get; set; }

    [Id(4)]
    public DateTime PeriodStart { get; set; }

    [Id(5)]
    public DateTime PeriodEnd { get; set; }

    [Id(6)]
    public DateTime GeneratedDate { get; set; }

    [Id(7)]
    public int TotalEvents { get; set; }

    [Id(8)]
    public string IntegrityStatus { get; set; } = string.Empty;
}

/// <summary>
/// Index state for all generated audit reports.
/// Grain Key: "AUDIT-REPORT-INDEX"
/// </summary>
[GenerateSerializer]
public class AuditReportIndexState
{
    [Id(0)]
    public List<AuditReportSummary> Reports { get; set; } = new();
}
