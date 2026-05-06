// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Status of a cancer registry report submission.
/// </summary>
[GenerateSerializer]
public enum CancerRegistryReportStatus
{
    /// <summary>Report generated but not yet submitted.</summary>
    Generated = 0,

    /// <summary>Report submitted to registry, awaiting response.</summary>
    Submitted = 1,

    /// <summary>Report accepted by the cancer registry.</summary>
    Accepted = 2,

    /// <summary>Report rejected by the cancer registry.</summary>
    Rejected = 3
}

/// <summary>
/// State for a cancer registry report (NAACCR abstract).
/// §170.315(f)(4) — Transmission to cancer registries.
///
/// Contains structured NAACCR data items extracted from oncology tumor/treatment
/// grains plus the generated abstract text.
///
/// Grain Key: "CR-REPORT:{reportId}"
/// </summary>
[GenerateSerializer]
public class CancerRegistryReportState
{
    /// <summary>Unique report identifier. (NAACCR Item #20)</summary>
    [Id(0)]
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Patient identifier. (NAACCR Item #20)</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Tumor identifier (references OncologyTumorGrain key).</summary>
    [Id(2)]
    public string TumorId { get; set; } = string.Empty;

    // ─── Patient Demographics (NAACCR Items #2230–#2380) ──────────────────

    /// <summary>Patient name. (NAACCR Item #2230)</summary>
    [Id(3)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Date of birth. (NAACCR Item #240)</summary>
    [Id(4)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Sex. (NAACCR Item #220)</summary>
    [Id(5)]
    public string Sex { get; set; } = string.Empty;

    /// <summary>Race. (NAACCR Item #160)</summary>
    [Id(6)]
    public string Race { get; set; } = string.Empty;

    /// <summary>Social Security Number (masked). (NAACCR Item #2320)</summary>
    [Id(7)]
    public string Ssn { get; set; } = string.Empty;

    // ─── Tumor Data (NAACCR Items #400–#830) ──────────────────────────────

    /// <summary>Primary site (ICD-O-3 topography). (NAACCR Item #400)</summary>
    [Id(8)]
    public string PrimarySite { get; set; } = string.Empty;

    /// <summary>Primary site text description.</summary>
    [Id(9)]
    public string PrimarySiteText { get; set; } = string.Empty;

    /// <summary>Histology (ICD-O-3 morphology). (NAACCR Item #522)</summary>
    [Id(10)]
    public string Histology { get; set; } = string.Empty;

    /// <summary>Histology text description.</summary>
    [Id(11)]
    public string HistologyText { get; set; } = string.Empty;

    /// <summary>Laterality. (NAACCR Item #410)</summary>
    [Id(12)]
    public string Laterality { get; set; } = string.Empty;

    /// <summary>Date of diagnosis. (NAACCR Item #390)</summary>
    [Id(13)]
    public DateTime DateOfDiagnosis { get; set; }

    /// <summary>Diagnostic confirmation / basis of diagnosis. (NAACCR Item #490)</summary>
    [Id(14)]
    public string DiagnosticConfirmation { get; set; } = string.Empty;

    /// <summary>Sequence number — central. (NAACCR Item #380)</summary>
    [Id(15)]
    public int SequenceNumber { get; set; }

    // ─── Staging (NAACCR Items #880–#1060) ────────────────────────────────

    /// <summary>Clinical T. (NAACCR Item #940)</summary>
    [Id(16)]
    public string? ClinicalT { get; set; }

    /// <summary>Clinical N. (NAACCR Item #950)</summary>
    [Id(17)]
    public string? ClinicalN { get; set; }

    /// <summary>Clinical M. (NAACCR Item #960)</summary>
    [Id(18)]
    public string? ClinicalM { get; set; }

    /// <summary>Pathologic T. (NAACCR Item #880)</summary>
    [Id(19)]
    public string? PathologicT { get; set; }

    /// <summary>Pathologic N. (NAACCR Item #890)</summary>
    [Id(20)]
    public string? PathologicN { get; set; }

    /// <summary>Pathologic M. (NAACCR Item #900)</summary>
    [Id(21)]
    public string? PathologicM { get; set; }

    /// <summary>Stage group (AJCC). (NAACCR Item #970)</summary>
    [Id(22)]
    public string? StageGroup { get; set; }

    /// <summary>SEER Summary Stage 2018. (NAACCR Item #759)</summary>
    [Id(23)]
    public string? SeerSummaryStage { get; set; }

    // ─── Treatment Summary (NAACCR Items #1290–#1640) ─────────────────────

    /// <summary>Comma-separated list of treatment types administered.</summary>
    [Id(24)]
    public string TreatmentSummary { get; set; } = string.Empty;

    /// <summary>First course of treatment start date. (NAACCR Item #1270)</summary>
    [Id(25)]
    public DateTime? FirstTreatmentDate { get; set; }

    // ─── Reporting Metadata ───────────────────────────────────────────────

    /// <summary>Reporting facility identifier (NPI or state code). (NAACCR Item #540)</summary>
    [Id(26)]
    public string ReportingFacility { get; set; } = string.Empty;

    /// <summary>Cancer registrar who prepared the abstract. (NAACCR Item #570)</summary>
    [Id(27)]
    public string RegistrarId { get; set; } = string.Empty;

    /// <summary>Registrar name.</summary>
    [Id(28)]
    public string RegistrarName { get; set; } = string.Empty;

    /// <summary>Report status (Generated, Submitted, Accepted, Rejected).</summary>
    [Id(29)]
    public CancerRegistryReportStatus Status { get; set; } = CancerRegistryReportStatus.Generated;

    /// <summary>Target cancer registry name.</summary>
    [Id(30)]
    public string? RegistryName { get; set; }

    /// <summary>Registry confirmation number (on submission).</summary>
    [Id(31)]
    public string? ConfirmationNumber { get; set; }

    /// <summary>Registry response content (on acceptance or rejection).</summary>
    [Id(32)]
    public string? RegistryResponse { get; set; }

    /// <summary>Rejection reason (if rejected).</summary>
    [Id(33)]
    public string? RejectionReason { get; set; }

    /// <summary>Generated NAACCR abstract content (pipe-delimited flat file format).</summary>
    [Id(34)]
    public string NaaccrAbstractContent { get; set; } = string.Empty;

    /// <summary>Date report was generated.</summary>
    [Id(35)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date report was submitted to registry.</summary>
    [Id(36)]
    public DateTime? SubmittedDate { get; set; }

    /// <summary>Date of last modification.</summary>
    [Id(37)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry for cancer registry report index listing.
/// </summary>
[GenerateSerializer]
public class CancerRegistryReportIndexEntry
{
    [Id(0)] public string ReportId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public string TumorId { get; set; } = string.Empty;
    [Id(4)] public string PrimarySiteText { get; set; } = string.Empty;
    [Id(5)] public DateTime DateOfDiagnosis { get; set; }
    [Id(6)] public CancerRegistryReportStatus Status { get; set; }
    [Id(7)] public string ReportingFacility { get; set; } = string.Empty;
    [Id(8)] public DateTime CreatedDate { get; set; }
    [Id(9)] public string? RegistryName { get; set; }
}

/// <summary>
/// Index state for cancer registry reports.
/// Grain Key: "CR-REPORT-INDEX"
/// </summary>
[GenerateSerializer]
public class CancerRegistryReportIndexState
{
    [Id(0)]
    public List<CancerRegistryReportIndexEntry> Reports { get; set; } = new();
}
