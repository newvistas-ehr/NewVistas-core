// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Case type for Anatomic Pathology — VistA File #63.08 (SP), #63.09 (CY), #63.19 (AU)
/// </summary>
[GenerateSerializer]
public enum APCaseType
{
    /// <summary>Surgical Pathology — tissue biopsy or excision (#63.08)</summary>
    SurgicalPathology = 0,

    /// <summary>Cytology — cell-based specimens (PAP, FNA, effusion) (#63.09)</summary>
    Cytology = 1,

    /// <summary>Autopsy — post-mortem examination (#63.19)</summary>
    Autopsy = 2
}

/// <summary>
/// Workflow status of an Anatomic Pathology case
/// </summary>
[GenerateSerializer]
public enum APCaseStatus
{
    /// <summary>Specimen received, not yet examined</summary>
    Received = 0,

    /// <summary>Gross description entered; microscopic pending</summary>
    InProgress = 1,

    /// <summary>Preliminary diagnosis issued</summary>
    Preliminary = 2,

    /// <summary>Final signed-out diagnosis</summary>
    Final = 3,

    /// <summary>Addendum appended to a final case</summary>
    Addendum = 4,

    /// <summary>Diagnosis amended (corrected) after sign-out</summary>
    Amended = 5,

    /// <summary>Case cancelled (specimen unacceptable, duplicate, etc.)</summary>
    Cancelled = 6
}

/// <summary>
/// Manner of death for autopsy cases
/// </summary>
[GenerateSerializer]
public enum MannerOfDeath
{
    Natural = 0,
    Accident = 1,
    Suicide = 2,
    Homicide = 3,
    Undetermined = 4
}

/// <summary>
/// State for an individual Anatomic Pathology case.
/// Maps to VistA LAB DATA file (#63) subfiles:
///   #63.08 Surgical Pathology
///   #63.09 Cytology
///   #63.19 Autopsy
/// MUMPS routines: LRAP.m, LRAPSC.m, LRAPACC.m, LRAPAU.m
/// </summary>
[GenerateSerializer]
public class AnatomicPathologyState
{
    /// <summary>Unique case identifier (grain primary key)</summary>
    [Id(0)]
    public string CaseId { get; set; } = string.Empty;

    /// <summary>Patient IEN — reference to PATIENT file (#2)</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Case type: SurgicalPathology, Cytology, or Autopsy</summary>
    [Id(2)]
    public APCaseType CaseType { get; set; } = APCaseType.SurgicalPathology;

    /// <summary>Accession number — lab-assigned identifier (e.g. SP-2024-00123)</summary>
    [Id(3)]
    public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Current workflow status of the case</summary>
    [Id(4)]
    public APCaseStatus Status { get; set; } = APCaseStatus.Received;

    // ─── Specimen Information ────────────────────────────────────────────────

    /// <summary>Anatomical source / body site of the specimen (.01 SP/CY node)</summary>
    [Id(5)]
    public string? SpecimenSource { get; set; }

    /// <summary>Specimen description as submitted by clinician</summary>
    [Id(6)]
    public string? SpecimenDescription { get; set; }

    /// <summary>Specimen type (e.g. Biopsy, Excision, Aspirate, Smear, Whole Body)</summary>
    [Id(7)]
    public string? SpecimenType { get; set; }

    /// <summary>Number of specimen parts/containers received</summary>
    [Id(8)]
    public int? SpecimenPartCount { get; set; }

    /// <summary>Specimen weight in grams (common for excision specimens)</summary>
    [Id(9)]
    public decimal? SpecimenWeightGrams { get; set; }

    // ─── Clinical Information ─────────────────────────────────────────────────

    /// <summary>Clinical history provided by ordering provider</summary>
    [Id(10)]
    public string? ClinicalHistory { get; set; }

    /// <summary>Pre-operative / clinical diagnosis supplied by clinician</summary>
    [Id(11)]
    public string? ClinicalDiagnosis { get; set; }

    /// <summary>IEN of requesting/ordering provider — PATIENT file (#2) (.14)</summary>
    [Id(12)]
    public string? ReferringProviderId { get; set; }

    /// <summary>Name of requesting/ordering provider</summary>
    [Id(13)]
    public string? ReferringProviderName { get; set; }

    /// <summary>Location/clinic the specimen was collected from</summary>
    [Id(14)]
    public string? CollectionLocation { get; set; }

    // ─── Dates ───────────────────────────────────────────────────────────────

    /// <summary>Date/time specimen was collected from patient</summary>
    [Id(15)]
    public DateTime? DateCollected { get; set; }

    /// <summary>Date/time specimen was received in the pathology lab</summary>
    [Id(16)]
    public DateTime? DateReceived { get; set; }

    /// <summary>Date/time accession number was assigned</summary>
    [Id(17)]
    public DateTime? DateAccessioned { get; set; }

    /// <summary>Date/time final report was issued</summary>
    [Id(18)]
    public DateTime? DateReported { get; set; }

    // ─── Gross Description (Macroscopic Examination) ─────────────────────────

    /// <summary>Gross/macroscopic description of the specimen by pathologist</summary>
    [Id(19)]
    public string? GrossDescription { get; set; }

    /// <summary>Pathologist who performed gross examination</summary>
    [Id(20)]
    public string? GrossPathologistId { get; set; }

    [Id(21)]
    public string? GrossPathologistName { get; set; }

    [Id(22)]
    public DateTime? GrossExamDateTime { get; set; }

    // ─── Microscopic Description ──────────────────────────────────────────────

    /// <summary>Microscopic/histologic description of tissue sections</summary>
    [Id(23)]
    public string? MicroscopicDescription { get; set; }

    // ─── Diagnosis ────────────────────────────────────────────────────────────

    /// <summary>Final pathological diagnosis (free text)</summary>
    [Id(24)]
    public string? Diagnosis { get; set; }

    /// <summary>ICD-10 diagnosis codes linked to this case</summary>
    [Id(25)]
    public List<string> DiagnosisCodes { get; set; } = new();

    /// <summary>Pathologist who signed out / finalized the case</summary>
    [Id(26)]
    public string? PathologistId { get; set; }

    [Id(27)]
    public string? PathologistName { get; set; }

    [Id(28)]
    public DateTime? SignOutDateTime { get; set; }

    // ─── Supplemental Studies ─────────────────────────────────────────────────

    /// <summary>Special stains ordered/performed (e.g. PAS, AFB, GMS)</summary>
    [Id(29)]
    public List<string> SpecialStains { get; set; } = new();

    /// <summary>Immunohistochemistry panel results</summary>
    [Id(30)]
    public List<string> ImmunohistochemistryResults { get; set; } = new();

    /// <summary>Frozen section diagnosis (if performed intra-operatively)</summary>
    [Id(31)]
    public string? FrozenSectionDiagnosis { get; set; }

    // ─── Cytology-Specific ────────────────────────────────────────────────────

    /// <summary>Bethesda system category for gynecologic cytology (PAP smears)</summary>
    [Id(32)]
    public string? BethesdaCategory { get; set; }

    /// <summary>Adequacy of the cytology specimen</summary>
    [Id(33)]
    public string? SpecimenAdequacy { get; set; }

    // ─── Autopsy-Specific ─────────────────────────────────────────────────────

    /// <summary>Immediate cause of death (Part I line a)</summary>
    [Id(34)]
    public string? CauseOfDeath { get; set; }

    /// <summary>Underlying cause of death (Part I line b/c/d)</summary>
    [Id(35)]
    public string? UnderlyingCauseOfDeath { get; set; }

    /// <summary>Manner of death classification</summary>
    [Id(36)]
    public MannerOfDeath? MannerOfDeath { get; set; }

    /// <summary>Toxicology findings summary</summary>
    [Id(37)]
    public string? ToxicologyFindings { get; set; }

    /// <summary>Body weight at autopsy in kilograms</summary>
    [Id(38)]
    public decimal? BodyWeightKg { get; set; }

    /// <summary>Neuropathology findings (brain examination)</summary>
    [Id(39)]
    public string? NeuropathologyFindings { get; set; }

    // ─── Addendum / Amendment ────────────────────────────────────────────────

    /// <summary>Addendum text appended after final sign-out</summary>
    [Id(40)]
    public string? Addendum { get; set; }

    [Id(41)]
    public DateTime? AddendumDate { get; set; }

    [Id(42)]
    public string? AddendumPathologistId { get; set; }

    [Id(43)]
    public string? AddendumPathologistName { get; set; }

    /// <summary>Amendment reason when diagnosis is corrected after sign-out</summary>
    [Id(44)]
    public string? AmendmentReason { get; set; }

    // ─── General ─────────────────────────────────────────────────────────────

    /// <summary>Free-text comments or notes on the case</summary>
    [Id(45)]
    public string? Comments { get; set; }

    /// <summary>Date/time record was created</summary>
    [Id(46)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time record was last modified</summary>
    [Id(47)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
