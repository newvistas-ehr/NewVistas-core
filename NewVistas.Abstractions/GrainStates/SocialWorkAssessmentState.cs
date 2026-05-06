// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Assessment type — mirrors VistA Social Work assessment categories (File #707).
/// </summary>
[GenerateSerializer]
public enum SocialWorkAssessmentType
{
    Psychosocial = 0,
    FunctionalStatus = 1,
    DischargeRisk = 2,
    HomelessRisk = 3,
    SubstanceUse = 4,
    DomesticViolence = 5,
    Bereavement = 6,
    CaregiverStress = 7,
    Other = 8,
}

/// <summary>
/// Lifecycle status of a social work assessment.
/// </summary>
[GenerateSerializer]
public enum SocialWorkAssessmentStatus
{
    Draft = 0,
    Complete = 1,
    Closed = 2,
}

/// <summary>
/// Clinician-assigned risk level resulting from the assessment.
/// </summary>
[GenerateSerializer]
public enum SocialWorkRiskLevel
{
    Unknown = 0,
    Low = 1,
    Moderate = 2,
    High = 3,
    Critical = 4,
}

/// <summary>
/// Persistent state for a Social Work Assessment grain.
/// VistA File #707 — SOCIAL WORK ASSESSMENT.
/// Mirrors SWRPATCH.m (Social Work routines).
/// </summary>
[GenerateSerializer]
public class SocialWorkAssessmentState
{
    /// <summary>
    /// Unique grain key (SW-ASSESSMENT:{guid}).
    /// </summary>
    [Id(0)]
    public string AssessmentId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (.01).
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Type of assessment (.02).
    /// </summary>
    [Id(2)]
    public SocialWorkAssessmentType AssessmentType { get; set; }

    /// <summary>
    /// Date/time assessment was conducted (.03).
    /// </summary>
    [Id(3)]
    public DateTime AssessmentDate { get; set; }

    /// <summary>
    /// Social worker DUZ (.04).
    /// </summary>
    [Id(4)]
    public string? SocialWorkerId { get; set; }

    /// <summary>
    /// Social worker name (.05).
    /// </summary>
    [Id(5)]
    public string? SocialWorkerName { get; set; }

    /// <summary>
    /// Overall risk level (.06).
    /// </summary>
    [Id(6)]
    public SocialWorkRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Lifecycle status (.07).
    /// </summary>
    [Id(7)]
    public SocialWorkAssessmentStatus Status { get; set; }

    // ── Psychosocial / Living Situation ──────────────────────────────────────

    /// <summary>
    /// Current housing status — e.g. HOUSED, HOMELESS, AT RISK (#707.03).
    /// </summary>
    [Id(8)]
    public string? HousingStatus { get; set; }

    /// <summary>
    /// Employment/vocational status — e.g. EMPLOYED, UNEMPLOYED, RETIRED.
    /// </summary>
    [Id(9)]
    public string? EmploymentStatus { get; set; }

    /// <summary>
    /// Adequacy of social support network — e.g. STRONG, ADEQUATE, POOR.
    /// </summary>
    [Id(10)]
    public string? SocialSupport { get; set; }

    /// <summary>
    /// Identified financial stressors (free text).
    /// </summary>
    [Id(11)]
    public string? FinancialStressors { get; set; }

    /// <summary>
    /// Substance use history findings (free text).
    /// </summary>
    [Id(12)]
    public string? SubstanceUseHistory { get; set; }

    /// <summary>
    /// Domestic violence / abuse concerns identified (true = concerns noted).
    /// </summary>
    [Id(13)]
    public bool? AbuseConcernsIdentified { get; set; }

    /// <summary>
    /// Safety plan in place when abuse concerns noted.
    /// </summary>
    [Id(14)]
    public bool? SafetyPlanInPlace { get; set; }

    // ── Discharge Planning ───────────────────────────────────────────────────

    /// <summary>
    /// Anticipated discharge date.
    /// </summary>
    [Id(15)]
    public DateTime? AnticipatedDischargeDate { get; set; }

    /// <summary>
    /// Planned discharge disposition — e.g. HOME, NURSING FACILITY, ASSISTED LIVING.
    /// </summary>
    [Id(16)]
    public string? DischargeDisposition { get; set; }

    /// <summary>
    /// Discharge plan summary (free text).
    /// </summary>
    [Id(17)]
    public string? DischargePlan { get; set; }

    /// <summary>
    /// Barriers to discharge identified.
    /// </summary>
    [Id(18)]
    public List<string> DischargeBarriers { get; set; } = new();

    // ── Assessment Findings ──────────────────────────────────────────────────

    /// <summary>
    /// Clinical recommendations documented by the social worker.
    /// </summary>
    [Id(19)]
    public string? Recommendations { get; set; }

    /// <summary>
    /// Narrative assessment notes.
    /// </summary>
    [Id(20)]
    public string? Notes { get; set; }

    /// <summary>
    /// Location/clinic where assessment was conducted.
    /// </summary>
    [Id(21)]
    public string? LocationId { get; set; }

    [Id(22)]
    public string? LocationName { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Date/time the assessment record was signed/completed.
    /// </summary>
    [Id(23)]
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Reason for closing the assessment without full completion.
    /// </summary>
    [Id(24)]
    public string? ClosedReason { get; set; }

    [Id(25)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(26)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry stored in the per-patient assessment index.
/// </summary>
[GenerateSerializer]
public class SocialWorkAssessmentIndexEntry
{
    [Id(0)]
    public string AssessmentId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public SocialWorkAssessmentType AssessmentType { get; set; }

    [Id(3)]
    public DateTime AssessmentDate { get; set; }

    [Id(4)]
    public string? SocialWorkerName { get; set; }

    [Id(5)]
    public SocialWorkRiskLevel RiskLevel { get; set; }

    [Id(6)]
    public SocialWorkAssessmentStatus Status { get; set; }

    [Id(7)]
    public string? HousingStatus { get; set; }
}

/// <summary>
/// State class for the per-patient assessment index grain (SW-ASSESSMENT-IDX:{patientId}).
/// </summary>
[GenerateSerializer]
public class SocialWorkAssessmentIndexState
{
    /// <summary>
    /// Ordered list of assessment summaries (newest first).
    /// </summary>
    [Id(0)]
    public List<SocialWorkAssessmentIndexEntry> Entries { get; set; } = new();
}
