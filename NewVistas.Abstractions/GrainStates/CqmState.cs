// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a Clinical Quality Measure (eCQM) definition.
/// §170.315(c)(1) — Record and export CQM data.
///
/// Maps to CMS eCQM specifications (e.g., CMS122v12, CMS165v12).
/// Each measure defines numerator/denominator/exclusion criteria that
/// evaluate against clinical data in NewVistas grains.
///
/// VistA equivalent: Clinical Reminders package + EPRP (External Peer Review Program).
///
/// Grain Key: "CQM:{measureId}" (e.g., "CQM:CMS122v12")
/// </summary>
[GenerateSerializer]
public class CqmMeasureState
{
    /// <summary>CMS measure identifier (e.g., "CMS122v12").</summary>
    [Id(0)]
    public string MeasureId { get; set; } = string.Empty;

    /// <summary>Human-readable title (e.g., "Diabetes: Hemoglobin A1c (HbA1c) Poor Control (> 9%)").</summary>
    [Id(1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of what the measure evaluates.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>NQF (National Quality Forum) number if assigned.</summary>
    [Id(3)]
    public string? NqfNumber { get; set; }

    /// <summary>eCQM version (e.g., "12" for CMS122v12).</summary>
    [Id(4)]
    public string Version { get; set; } = string.Empty;

    /// <summary>Measure steward (e.g., "National Committee for Quality Assurance").</summary>
    [Id(5)]
    public string Steward { get; set; } = string.Empty;

    /// <summary>Measure type: "proportion", "continuous-variable", "ratio".</summary>
    [Id(6)]
    public string MeasureType { get; set; } = "proportion";

    /// <summary>Clinical domain: "diabetes", "hypertension", "preventive", "behavioral", etc.</summary>
    [Id(7)]
    public string ClinicalDomain { get; set; } = string.Empty;

    /// <summary>
    /// Initial Population criteria — defines which patients are eligible for the measure.
    /// Each criterion specifies a data source and matching rule.
    /// </summary>
    [Id(8)]
    public List<CqmCriterion> InitialPopulation { get; set; } = new();

    /// <summary>
    /// Denominator criteria — patients in the initial population who are eligible
    /// for the performance rate calculation.
    /// </summary>
    [Id(9)]
    public List<CqmCriterion> Denominator { get; set; } = new();

    /// <summary>
    /// Denominator Exclusion criteria — patients removed from the denominator
    /// (e.g., hospice patients, pregnant women for certain measures).
    /// </summary>
    [Id(10)]
    public List<CqmCriterion> DenominatorExclusions { get; set; } = new();

    /// <summary>
    /// Numerator criteria — patients who met the quality action
    /// (e.g., HbA1c was tested and result ≤ 9%).
    /// </summary>
    [Id(11)]
    public List<CqmCriterion> Numerator { get; set; } = new();

    /// <summary>
    /// Numerator Exclusion criteria — if any, patients removed from the numerator.
    /// </summary>
    [Id(12)]
    public List<CqmCriterion> NumeratorExclusions { get; set; } = new();

    /// <summary>Whether this measure is active for reporting.</summary>
    [Id(13)]
    public bool IsActive { get; set; } = true;

    /// <summary>Reporting programs this measure applies to (e.g., "MIPS", "CPC+", "ACO").</summary>
    [Id(14)]
    public List<string> ReportingPrograms { get; set; } = new();

    [Id(15)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single criterion within a CQM population definition.
/// Criteria evaluate patient data from specific clinical domains.
/// </summary>
[GenerateSerializer]
public class CqmCriterion
{
    /// <summary>Data source to evaluate: "Problem", "Lab", "Vital", "Medication", "Encounter", "Procedure", "Demographic".</summary>
    [Id(0)]
    public string DataSource { get; set; } = string.Empty;

    /// <summary>
    /// Value set or code to match. For problems: ICD-10 codes (e.g., "E11.*").
    /// For labs: LOINC codes (e.g., "4548-4" for HbA1c).
    /// For demographics: field name (e.g., "Age").
    /// </summary>
    [Id(1)]
    public string ValueSetOrCode { get; set; } = string.Empty;

    /// <summary>Description of what this criterion checks.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Comparison operator: "exists", "not-exists", "equals", "greater-than", "less-than", "between", "starts-with".</summary>
    [Id(3)]
    public string Operator { get; set; } = "exists";

    /// <summary>Comparison value (for numeric/date comparisons).</summary>
    [Id(4)]
    public string? ComparisonValue { get; set; }

    /// <summary>Secondary comparison value (for "between" operator).</summary>
    [Id(5)]
    public string? ComparisonValue2 { get; set; }

    /// <summary>Whether this criterion must be within the measurement period.</summary>
    [Id(6)]
    public bool RequireInMeasurementPeriod { get; set; } = true;
}

/// <summary>
/// State for a CQM evaluation report — the result of evaluating a measure
/// across a set of patients for a reporting period.
/// §170.315(c)(2) — Import and calculate.
/// §170.315(c)(3) — Report (QRDA III).
///
/// Grain Key: "CQM-REPORT:{reportId}"
/// </summary>
[GenerateSerializer]
public class CqmReportState
{
    [Id(0)]
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Measure being evaluated (e.g., "CMS122v12").</summary>
    [Id(1)]
    public string MeasureId { get; set; } = string.Empty;

    /// <summary>Reporting period start date.</summary>
    [Id(2)]
    public DateTime PeriodStart { get; set; }

    /// <summary>Reporting period end date.</summary>
    [Id(3)]
    public DateTime PeriodEnd { get; set; }

    /// <summary>Status: "pending", "evaluating", "completed", "error".</summary>
    [Id(4)]
    public string Status { get; set; } = "pending";

    /// <summary>Per-patient evaluation results.</summary>
    [Id(5)]
    public List<CqmPatientResult> PatientResults { get; set; } = new();

    // ─── Aggregate Results ───────────────────────────────────────────────

    /// <summary>Count of patients in the initial population.</summary>
    [Id(6)]
    public int InitialPopulationCount { get; set; }

    /// <summary>Count of patients in the denominator.</summary>
    [Id(7)]
    public int DenominatorCount { get; set; }

    /// <summary>Count of patients excluded from the denominator.</summary>
    [Id(8)]
    public int DenominatorExclusionCount { get; set; }

    /// <summary>Count of patients in the numerator (met the quality action).</summary>
    [Id(9)]
    public int NumeratorCount { get; set; }

    /// <summary>Performance rate = Numerator / (Denominator - DenominatorExclusions).</summary>
    [Id(10)]
    public double PerformanceRate { get; set; }

    [Id(11)]
    public DateTime? EvaluatedDate { get; set; }

    [Id(12)]
    public string? ErrorMessage { get; set; }

    /// <summary>Patient IDs evaluated (for re-runs and filtering).</summary>
    [Id(13)]
    public List<string> EvaluatedPatientIds { get; set; } = new();

    /// <summary>Who initiated the evaluation.</summary>
    [Id(14)]
    public string? EvaluatedBy { get; set; }

    [Id(15)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-patient result within a CQM evaluation report.
/// Contains enough data to generate QRDA Category I for this patient.
/// </summary>
[GenerateSerializer]
public class CqmPatientResult
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Whether the patient is in the initial population.</summary>
    [Id(2)]
    public bool InInitialPopulation { get; set; }

    /// <summary>Whether the patient is in the denominator.</summary>
    [Id(3)]
    public bool InDenominator { get; set; }

    /// <summary>Whether the patient is excluded from the denominator.</summary>
    [Id(4)]
    public bool IsDenominatorExclusion { get; set; }

    /// <summary>Whether the patient met the numerator criteria (quality action).</summary>
    [Id(5)]
    public bool InNumerator { get; set; }

    /// <summary>Clinical evidence supporting the evaluation (e.g., "HbA1c 7.2% on 2026-01-15").</summary>
    [Id(6)]
    public List<string> Evidence { get; set; } = new();

    /// <summary>Reason for exclusion, if applicable.</summary>
    [Id(7)]
    public string? ExclusionReason { get; set; }

    // ─── Demographic data for §170.315(c)(4) filtering ───────────────────

    [Id(8)]
    public int? Age { get; set; }

    [Id(9)]
    public string? Sex { get; set; }

    [Id(10)]
    public string? Race { get; set; }

    [Id(11)]
    public string? Ethnicity { get; set; }

    [Id(12)]
    public string? Payer { get; set; }
}

/// <summary>
/// Index entry for listing CQM measures.
/// </summary>
[GenerateSerializer]
public class CqmMeasureSummary
{
    [Id(0)]
    public string MeasureId { get; set; } = string.Empty;

    [Id(1)]
    public string Title { get; set; } = string.Empty;

    [Id(2)]
    public string ClinicalDomain { get; set; } = string.Empty;

    [Id(3)]
    public string MeasureType { get; set; } = string.Empty;

    [Id(4)]
    public bool IsActive { get; set; }

    [Id(5)]
    public string? NqfNumber { get; set; }
}

/// <summary>
/// Index state for listing all CQM measures.
/// Grain Key: "CQM-INDEX"
/// </summary>
[GenerateSerializer]
public class CqmMeasureIndexState
{
    [Id(0)]
    public List<CqmMeasureSummary> Measures { get; set; } = new();
}

/// <summary>
/// §170.315(c)(4) — Filter results for a CQM report by demographic criteria.
/// </summary>
[GenerateSerializer]
public class CqmFilterCriteria
{
    [Id(0)]
    public int? MinAge { get; set; }

    [Id(1)]
    public int? MaxAge { get; set; }

    [Id(2)]
    public string? Sex { get; set; }

    [Id(3)]
    public string? Race { get; set; }

    [Id(4)]
    public string? Ethnicity { get; set; }

    [Id(5)]
    public string? Payer { get; set; }
}
