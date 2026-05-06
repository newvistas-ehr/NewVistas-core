// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Clinical Quality Measure Definition Grain — stores an eCQM specification.
/// §170.315(c)(1) — Record and export CQM data.
///
/// Maps to CMS eCQM specifications. Each measure defines populations
/// (initial population, denominator, numerator, exclusions) with criteria
/// that evaluate against NewVistas clinical data grains.
///
/// VistA equivalent: Clinical Reminders definitions + EPRP measure templates.
///
/// Grain Key: "CQM:{measureId}" (e.g., "CQM:CMS122v12")
/// </summary>
public interface ICqmMeasureGrain : IGrainWithStringKey
{
    /// <summary>Define or update a quality measure.</summary>
    Task SaveMeasureAsync(CqmMeasureState measure);

    /// <summary>Get the full measure definition.</summary>
    Task<CqmMeasureState> GetMeasureAsync();

    /// <summary>Activate/deactivate the measure for reporting.</summary>
    Task SetActiveAsync(bool isActive);
}

/// <summary>
/// CQM Measure Index Grain — lists all registered quality measures.
///
/// Grain Key: "CQM-INDEX"
/// </summary>
public interface ICqmMeasureIndexGrain : IGrainWithStringKey
{
    Task AddMeasureAsync(CqmMeasureSummary summary);
    Task RemoveMeasureAsync(string measureId);
    Task<List<CqmMeasureSummary>> GetAllMeasuresAsync();
    Task<List<CqmMeasureSummary>> GetActiveMeasuresAsync();
}

/// <summary>
/// CQM Evaluation Report Grain — evaluates a quality measure across patients
/// and generates QRDA Category I (patient-level) and Category III (aggregate) exports.
///
/// §170.315(c)(1) — Record and export (QRDA I)
/// §170.315(c)(2) — Import and calculate
/// §170.315(c)(3) — Report (QRDA III)
/// §170.315(c)(4) — Filter by demographics
///
/// Grain Key: "CQM-REPORT:{reportId}"
/// </summary>
public interface ICqmReportGrain : IGrainWithStringKey
{
    /// <summary>
    /// Evaluate a measure across a list of patients for a reporting period.
    /// Populates PatientResults and computes aggregate counts.
    /// </summary>
    Task EvaluateAsync(
        string measureId,
        List<string> patientIds,
        DateTime periodStart,
        DateTime periodEnd,
        string? evaluatedBy);

    /// <summary>Get the full report state (results + aggregates).</summary>
    Task<CqmReportState> GetReportAsync();

    /// <summary>
    /// Get filtered aggregate results per §170.315(c)(4).
    /// Filters patient results by demographics and recomputes aggregates.
    /// </summary>
    Task<CqmReportState> GetFilteredReportAsync(CqmFilterCriteria filter);

    /// <summary>
    /// Generate QRDA Category I XML for a specific patient.
    /// §170.315(c)(1) — patient-level CQM export.
    /// </summary>
    Task<string> ExportQrdaCategoryIAsync(string patientId);

    /// <summary>
    /// Generate QRDA Category III XML for the aggregate report.
    /// §170.315(c)(3) — population-level quality reporting.
    /// </summary>
    Task<string> ExportQrdaCategoryIIIAsync();

    /// <summary>
    /// Generate filtered QRDA Category III XML.
    /// §170.315(c)(4) — filtered aggregate reporting.
    /// </summary>
    Task<string> ExportFilteredQrdaCategoryIIIAsync(CqmFilterCriteria filter);
}
