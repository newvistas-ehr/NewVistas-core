// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ──────────────────────────────────────────────────────────────────────────
// Enumerations
// ──────────────────────────────────────────────────────────────────────────

/// <summary>Category of occurrence or patient safety event (VistA File #680.1).</summary>
[GenerateSerializer]
public enum OccurrenceCategory
{
    MedicationError,
    FallWithInjury,
    ProcedureComplication,
    SurgicalError,
    DiagnosticError,
    InfectionEvent,
    EquipmentFailure,
    BehavioralHealthIncident,
    PatientElopement,
    SuicideAttemptSelfHarm,
    DelayInTreatment,
    PressureUlcer,
    TransfusionReaction,
    Other
}

/// <summary>Harm severity level of the occurrence (based on NCC MERP / VA PSO taxonomy).</summary>
[GenerateSerializer]
public enum OccurrenceSeverity
{
    NearMiss,
    NoHarm,
    MinorHarm,
    ModerateHarm,
    SevereHarm,
    Death
}

/// <summary>Workflow status of an incident / occurrence screen report.</summary>
[GenerateSerializer]
public enum IncidentStatus
{
    Reported,
    UnderReview,
    PeerReviewAssigned,
    RCAInProgress,
    Closed,
    Voided
}

/// <summary>Type of quality review being conducted.</summary>
[GenerateSerializer]
public enum QMReviewType
{
    PeerReview,
    RootCauseAnalysis,
    AggregateReview,
    FocusedProfessionalPracticeEvaluation,
    ExternalReview
}

/// <summary>Workflow status of a quality review.</summary>
[GenerateSerializer]
public enum QMReviewStatus
{
    Pending,
    InProgress,
    Completed,
    Approved,
    Archived
}

/// <summary>Primary finding category from a peer review or RCA.</summary>
[GenerateSerializer]
public enum ReviewFinding
{
    NoIssueFound,
    SystemIssue,
    ProcessIssue,
    HumanFactors,
    EquipmentIssue,
    EnvironmentalIssue,
    CommunicationBreakdown,
    TrainingKnowledgeGap,
    MultiFactorial
}

/// <summary>Status of an individual corrective action item.</summary>
[GenerateSerializer]
public enum ActionItemStatus
{
    Pending,
    InProgress,
    Completed,
    Overdue,
    Cancelled
}

// ──────────────────────────────────────────────────────────────────────────
// QMActionItem — corrective action nested record
// ──────────────────────────────────────────────────────────────────────────

/// <summary>An individual corrective action item within a quality review.</summary>
[GenerateSerializer]
public class QMActionItem
{
    /// <summary>Unique action item identifier.</summary>
    [Id(0)] public string ActionId { get; set; } = string.Empty;

    /// <summary>Description of the corrective action required.</summary>
    [Id(1)] public string Description { get; set; } = string.Empty;

    /// <summary>Name or team responsible for completing the action.</summary>
    [Id(2)] public string AssignedTo { get; set; } = string.Empty;

    /// <summary>Target completion date.</summary>
    [Id(3)] public DateTime DueDate { get; set; }

    /// <summary>Current status of the action item.</summary>
    [Id(4)] public ActionItemStatus Status { get; set; } = ActionItemStatus.Pending;

    /// <summary>Date the action item was completed.</summary>
    [Id(5)] public DateTime? CompletedDate { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────
// QMIncidentState — VistA File #680 (OCCURRENCE SCREEN)
// ──────────────────────────────────────────────────────────────────────────

/// <summary>State of a single occurrence screen / patient safety incident report.</summary>
[GenerateSerializer]
public class QMIncidentState
{
    /// <summary>Unique incident identifier. (.01) QM-INCIDENT:{guid}</summary>
    [Id(0)] public string IncidentId { get; set; } = string.Empty;

    /// <summary>Patient DFN / ICN involved in the incident. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name. (.03)</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date and time the occurrence took place. (.04)</summary>
    [Id(3)] public DateTime OccurrenceDate { get; set; }

    /// <summary>Date and time the incident was reported. (.05)</summary>
    [Id(4)] public DateTime ReportedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Name of the staff member who reported the incident. (.06)</summary>
    [Id(5)] public string ReportedBy { get; set; } = string.Empty;

    /// <summary>Professional title of the reporter. (.07)</summary>
    [Id(6)] public string ReportedByTitle { get; set; } = string.Empty;

    /// <summary>Occurrence category / type. (.08)</summary>
    [Id(7)] public OccurrenceCategory Category { get; set; } = OccurrenceCategory.Other;

    /// <summary>Detailed narrative description of the incident. (.09)</summary>
    [Id(8)] public string Description { get; set; } = string.Empty;

    /// <summary>Physical location where the incident occurred. (.10)</summary>
    [Id(9)] public string Location { get; set; } = string.Empty;

    /// <summary>Ward or unit where the incident occurred. (.11)</summary>
    [Id(10)] public string WardUnit { get; set; } = string.Empty;

    /// <summary>Harm severity level. (.12)</summary>
    [Id(11)] public OccurrenceSeverity Severity { get; set; } = OccurrenceSeverity.NoHarm;

    /// <summary>Current workflow status of the report. (.13)</summary>
    [Id(12)] public IncidentStatus Status { get; set; } = IncidentStatus.Reported;

    /// <summary>Patient diagnosis active at the time of the incident. (.14)</summary>
    [Id(13)] public string DiagnosisAtTime { get; set; } = string.Empty;

    /// <summary>Procedure being performed at the time (if applicable). (.15)</summary>
    [Id(14)] public string ProcedureAtTime { get; set; } = string.Empty;

    /// <summary>Medication involved (drug name, dose) if a medication event. (.16)</summary>
    [Id(15)] public string MedicationInvolved { get; set; } = string.Empty;

    /// <summary>Equipment or device involved (if applicable). (.17)</summary>
    [Id(16)] public string EquipmentInvolved { get; set; } = string.Empty;

    /// <summary>Names of staff involved in or witnessing the incident. (.18)</summary>
    [Id(17)] public List<string> StaffInvolved { get; set; } = new();

    /// <summary>Names of witnesses to the incident. (.19)</summary>
    [Id(18)] public List<string> WitnessNames { get; set; } = new();

    /// <summary>Immediate actions taken at the time of the incident. (.20)</summary>
    [Id(19)] public string ImmediateAction { get; set; } = string.Empty;

    /// <summary>Clinical outcome and sequelae from the incident. (.21)</summary>
    [Id(20)] public string OutcomeDescription { get; set; } = string.Empty;

    /// <summary>Whether the patient was notified of the occurrence. (.22)</summary>
    [Id(21)] public bool PatientNotified { get; set; }

    /// <summary>Whether family/next-of-kin were notified. (.23)</summary>
    [Id(22)] public bool FamilyNotified { get; set; }

    /// <summary>List of review IDs associated with this incident. (.24)</summary>
    [Id(23)] public List<string> ReviewIds { get; set; } = new();

    /// <summary>Whether root cause has been formally identified. (.25)</summary>
    [Id(24)] public bool RootCauseIdentified { get; set; }

    /// <summary>Summary of corrective actions planned or completed. (.26)</summary>
    [Id(25)] public string CorrectiveActionsSummary { get; set; } = string.Empty;

    /// <summary>Reason for voiding (if Status = Voided). (.27)</summary>
    [Id(26)] public string VoidReason { get; set; } = string.Empty;

    /// <summary>Date/time this incident record was created. (.28)</summary>
    [Id(27)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time this incident record was last modified. (.29)</summary>
    [Id(28)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time this incident was closed. (.30)</summary>
    [Id(29)] public DateTime? ClosedDate { get; set; }
}

/// <summary>Summary entry stored in the system-wide incident index.</summary>
[GenerateSerializer]
public class QMIncidentIndexEntry
{
    /// <summary>Unique incident identifier.</summary>
    [Id(0)] public string IncidentId { get; set; } = string.Empty;

    /// <summary>Patient DFN / ICN.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date of occurrence.</summary>
    [Id(3)] public DateTime OccurrenceDate { get; set; }

    /// <summary>Occurrence category.</summary>
    [Id(4)] public OccurrenceCategory Category { get; set; }

    /// <summary>Harm severity level.</summary>
    [Id(5)] public OccurrenceSeverity Severity { get; set; }

    /// <summary>Current workflow status.</summary>
    [Id(6)] public IncidentStatus Status { get; set; }

    /// <summary>Physical location.</summary>
    [Id(7)] public string Location { get; set; } = string.Empty;

    /// <summary>Ward or unit.</summary>
    [Id(8)] public string WardUnit { get; set; } = string.Empty;

    /// <summary>Number of reviews linked to this incident.</summary>
    [Id(9)] public int ReviewCount { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────
// QMReviewState — Peer Review / Root Cause Analysis
// ──────────────────────────────────────────────────────────────────────────

/// <summary>State of a single peer review or root cause analysis record.</summary>
[GenerateSerializer]
public class QMReviewState
{
    /// <summary>Unique review identifier. (.01) QM-REVIEW:{guid}</summary>
    [Id(0)] public string ReviewId { get; set; } = string.Empty;

    /// <summary>Linked incident identifier. (.02)</summary>
    [Id(1)] public string IncidentId { get; set; } = string.Empty;

    /// <summary>Type of quality review. (.03)</summary>
    [Id(2)] public QMReviewType ReviewType { get; set; } = QMReviewType.PeerReview;

    /// <summary>Current workflow status. (.04)</summary>
    [Id(3)] public QMReviewStatus Status { get; set; } = QMReviewStatus.Pending;

    /// <summary>User/team the review is assigned to. (.05)</summary>
    [Id(4)] public string AssignedTo { get; set; } = string.Empty;

    /// <summary>Date the review was assigned. (.06)</summary>
    [Id(5)] public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Due date for completion of the review. (.07)</summary>
    [Id(6)] public DateTime DueDate { get; set; }

    /// <summary>Date the review was completed. (.08)</summary>
    [Id(7)] public DateTime? CompletedDate { get; set; }

    /// <summary>Name of the primary reviewer. (.09)</summary>
    [Id(8)] public string ReviewerName { get; set; } = string.Empty;

    /// <summary>Professional title of the reviewer. (.10)</summary>
    [Id(9)] public string ReviewerTitle { get; set; } = string.Empty;

    /// <summary>Whether the review is confidential peer-review protected. (.11)</summary>
    [Id(10)] public bool Confidential { get; set; } = true;

    /// <summary>Executive summary of the review. (.12)</summary>
    [Id(11)] public string Summary { get; set; } = string.Empty;

    /// <summary>Primary finding category. (.13)</summary>
    [Id(12)] public ReviewFinding PrimaryFinding { get; set; } = ReviewFinding.NoIssueFound;

    /// <summary>List of contributing factors identified. (.14)</summary>
    [Id(13)] public List<string> ContributingFactors { get; set; } = new();

    /// <summary>Root cause statement (for RCA reviews). (.15)</summary>
    [Id(14)] public string RootCause { get; set; } = string.Empty;

    /// <summary>System-level failures identified. (.16)</summary>
    [Id(15)] public List<string> SystemFailures { get; set; } = new();

    /// <summary>Human factors narrative. (.17)</summary>
    [Id(16)] public string HumanFactors { get; set; } = string.Empty;

    /// <summary>Environmental / situational factors narrative. (.18)</summary>
    [Id(17)] public string EnvironmentalFactors { get; set; } = string.Empty;

    /// <summary>Formal recommendations from the review. (.19)</summary>
    [Id(18)] public List<string> Recommendations { get; set; } = new();

    /// <summary>Corrective action items assigned from this review. (.20)</summary>
    [Id(19)] public List<QMActionItem> ActionItems { get; set; } = new();

    /// <summary>Final conclusion statement. (.21)</summary>
    [Id(20)] public string FinalConclusion { get; set; } = string.Empty;

    /// <summary>Key lessons learned to share system-wide. (.22)</summary>
    [Id(21)] public string LessonsLearned { get; set; } = string.Empty;

    /// <summary>Date/time this review record was created. (.23)</summary>
    [Id(22)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time this review record was last modified. (.24)</summary>
    [Id(23)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the system-wide review index.</summary>
[GenerateSerializer]
public class QMReviewIndexEntry
{
    /// <summary>Unique review identifier.</summary>
    [Id(0)] public string ReviewId { get; set; } = string.Empty;

    /// <summary>Linked incident identifier.</summary>
    [Id(1)] public string IncidentId { get; set; } = string.Empty;

    /// <summary>Type of quality review.</summary>
    [Id(2)] public QMReviewType ReviewType { get; set; }

    /// <summary>Current workflow status.</summary>
    [Id(3)] public QMReviewStatus Status { get; set; }

    /// <summary>Assigned reviewer name.</summary>
    [Id(4)] public string ReviewerName { get; set; } = string.Empty;

    /// <summary>User/team the review is assigned to.</summary>
    [Id(5)] public string AssignedTo { get; set; } = string.Empty;

    /// <summary>Due date.</summary>
    [Id(6)] public DateTime DueDate { get; set; }

    /// <summary>Completed date.</summary>
    [Id(7)] public DateTime? CompletedDate { get; set; }

    /// <summary>Number of action items in this review.</summary>
    [Id(8)] public int ActionItemCount { get; set; }
}
