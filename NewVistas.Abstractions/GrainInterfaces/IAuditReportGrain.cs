// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Audit Report Grain — stores a generated audit report.
/// §170.315(d)(3) — Audit Report(s).
///
/// Grain Key: "AUDIT-REPORT:{reportId}"
/// </summary>
public interface IAuditReportGrain : IGrainWithStringKey
{
    Task SaveReportAsync(AuditReportState report);
    Task<AuditReportState> GetReportAsync();
}

/// <summary>
/// Audit Report Index Grain — listing of all generated audit reports.
/// Grain Key: "AUDIT-REPORT-INDEX"
/// </summary>
public interface IAuditReportIndexGrain : IGrainWithStringKey
{
    Task AddReportAsync(AuditReportSummary summary);
    Task<List<AuditReportSummary>> GetAllReportsAsync();
    Task<List<AuditReportSummary>> GetReportsByPatientAsync(string patientId, int maxResults = 50);
    Task<List<AuditReportSummary>> GetReportsByTypeAsync(string reportType);
}

/// <summary>
/// Audit Report Generator Grain — generates a formal audit report for a patient.
/// Reads the patient's audit index, applies filters, computes aggregation stats,
/// and optionally verifies hash-chain integrity.
///
/// Grain Key: "AUDIT-REPORT-GEN:{patientId}"
/// </summary>
public interface IAuditReportGeneratorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generate a formal audit report for the patient.
    /// </summary>
    Task<AuditReportState> GenerateReportAsync(
        DateTime periodStart,
        DateTime periodEnd,
        string? domainFilter,
        string? actionFilter,
        string? userIdFilter,
        bool verifyIntegrity,
        string? generatedBy);
}
