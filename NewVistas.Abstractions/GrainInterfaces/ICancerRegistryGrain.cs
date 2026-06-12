// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Cancer Registry Report Grain — generates and tracks NAACCR abstract records
/// for submission to state/central cancer registries.
/// §170.315(f)(4) — Transmission to cancer registries.
///
/// NAACCR (North American Association of Central Cancer Registries) abstracts contain
/// standardized data items covering demographics, tumor characteristics, staging,
/// treatment, and follow-up information.
///
/// Grain Key: "CR-REPORT:{reportId}"
/// </summary>
public interface ICancerRegistryReportGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this cancer registry report.</summary>
    Task<CancerRegistryReportState> GetReportAsync();

    /// <summary>
    /// Generates a NAACCR abstract from the specified oncology tumor data.
    /// Populates all standard NAACCR data items from tumor, treatment, and patient demographics.
    /// </summary>
    Task GenerateReportAsync(
        string patientId,
        string tumorId,
        string reportingFacility,
        string registrarId,
        string registrarName);

    /// <summary>Marks the report as submitted to the cancer registry.</summary>
    Task SubmitReportAsync(string registryName, string? confirmationNumber);

    /// <summary>Marks the report as accepted by the cancer registry.</summary>
    Task AcceptReportAsync(string? registryResponse);

    /// <summary>Marks the report as rejected with a reason.</summary>
    Task RejectReportAsync(string rejectionReason);

    /// <summary>Retrieves the generated NAACCR abstract content.</summary>
    Task<string> GetNaaccrAbstractAsync();
}

/// <summary>
/// Cancer Registry Report Index Grain — tracks all cancer registry reports.
/// System-level grain for listing and filtering reports by patient, status, or facility.
///
/// Grain Key: "CR-REPORT-INDEX"
/// </summary>
public interface ICancerRegistryReportIndexGrain : IGrainWithStringKey
{
    /// <summary>Add or update a report entry in the index.</summary>
    Task UpsertReportAsync(CancerRegistryReportIndexEntry entry);

    /// <summary>Get all reports.</summary>
    Task<List<CancerRegistryReportIndexEntry>> GetAllReportsAsync();

    /// <summary>Get reports by patient.</summary>
    /// <param name="maxResults">Maximum entries returned, newest first. Bounds the payload; full history is available by passing a larger value.</param>
    Task<List<CancerRegistryReportIndexEntry>> GetReportsByPatientAsync(string patientId, int maxResults = 50);

    /// <summary>Get reports by status.</summary>
    Task<List<CancerRegistryReportIndexEntry>> GetReportsByStatusAsync(string status);

    /// <summary>Get reports pending submission.</summary>
    Task<List<CancerRegistryReportIndexEntry>> GetPendingReportsAsync();
}
