// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ──────────────────────────────────────────────────────────────────────────
// Enumerations
// ──────────────────────────────────────────────────────────────────────────

/// <summary>Status of a Compensation &amp; Pension examination (VistA File #396).</summary>
[GenerateSerializer]
public enum CPExamStatus
{
    Scheduled,
    Completed,
    NoShow,
    Cancelled,
    Rescheduled
}

/// <summary>Type of C&amp;P examination requested by VBA.</summary>
[GenerateSerializer]
public enum CPExamType
{
    Initial,
    Increase,
    Review,
    Reexamination,
    TemporaryTotal,
    InformalHearing,
    DirectService,
    Other
}

/// <summary>Examiner category performing the C&amp;P evaluation.</summary>
[GenerateSerializer]
public enum CPExaminerType
{
    VAPhysician,
    ContractExaminer,
    QTCManagement,
    LHI,
    VES,
    OptumServe,
    Other
}

/// <summary>Type of Disability Benefits Questionnaire (DBQ) form.</summary>
[GenerateSerializer]
public enum DBQType
{
    GeneralMedical,
    PTSD,
    TBI,
    Musculoskeletal,
    Spine,
    MentalDisorders,
    Cardiovascular,
    Neurological,
    Scars,
    HearingLoss,
    Eyes,
    Respiratory,
    GenitoUrinary,
    Infectious,
    Other
}

/// <summary>Workflow status of a Disability Benefits Questionnaire.</summary>
[GenerateSerializer]
public enum DBQStatus
{
    Draft,
    Completed,
    Signed,
    Amended
}

/// <summary>Service-connection basis for a claimed condition.</summary>
[GenerateSerializer]
public enum ServiceConnectionType
{
    Pending,
    DirectService,
    PreexistingAggravated,
    Presumptive,
    Secondary,
    NotServiceConnected
}

// ──────────────────────────────────────────────────────────────────────────
// CPExamState — VistA File #396 (COMPENSATION AND PENSION EXAMINATION)
// ──────────────────────────────────────────────────────────────────────────

/// <summary>State of a single Compensation &amp; Pension examination record.</summary>
[GenerateSerializer]
public class CPExamState
{
    /// <summary>Unique exam identifier. (.01) CP-EXAM:{guid}</summary>
    [Id(0)] public string ExamId { get; set; } = string.Empty;

    /// <summary>Patient DFN / ICN. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name. (.03)</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Type of C&amp;P examination requested. (.04)</summary>
    [Id(3)] public CPExamType ExamType { get; set; } = CPExamType.Initial;

    /// <summary>Current workflow status of the exam. (.05)</summary>
    [Id(4)] public CPExamStatus Status { get; set; } = CPExamStatus.Scheduled;

    /// <summary>Date and time the exam is scheduled. (.06)</summary>
    [Id(5)] public DateTime ScheduledDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date and time the exam was completed. (.07)</summary>
    [Id(6)] public DateTime? CompletedDate { get; set; }

    /// <summary>Date and time the exam was cancelled. (.08)</summary>
    [Id(7)] public DateTime? CancelledDate { get; set; }

    /// <summary>Reason for cancellation or rescheduling. (.09)</summary>
    [Id(8)] public string CancellationReason { get; set; } = string.Empty;

    /// <summary>VA or VBA claim number linked to this exam. (.10)</summary>
    [Id(9)] public string ClaimNumber { get; set; } = string.Empty;

    /// <summary>Benefit type: Compensation or Pension. (.11)</summary>
    [Id(10)] public string BenefitType { get; set; } = string.Empty;

    /// <summary>Examiner full name. (.12)</summary>
    [Id(11)] public string ExaminerName { get; set; } = string.Empty;

    /// <summary>Examiner professional title (e.g., MD, DO, NP). (.13)</summary>
    [Id(12)] public string ExaminerTitle { get; set; } = string.Empty;

    /// <summary>Category of examiner performing the evaluation. (.14)</summary>
    [Id(13)] public CPExaminerType ExaminerType { get; set; } = CPExaminerType.VAPhysician;

    /// <summary>Clinic or room where exam takes place. (.15)</summary>
    [Id(14)] public string ExamLocation { get; set; } = string.Empty;

    /// <summary>Facility name where exam is performed. (.16)</summary>
    [Id(15)] public string ExamFacility { get; set; } = string.Empty;

    /// <summary>ICD-10 or free-text codes for conditions being claimed. (.17)</summary>
    [Id(16)] public List<string> DisabilityClaimedCodes { get; set; } = new();

    /// <summary>Final diagnoses recorded at exam completion. (.18)</summary>
    [Id(17)] public List<string> Diagnoses { get; set; } = new();

    /// <summary>Whether the examiner opined a nexus between service and condition. (.19)</summary>
    [Id(18)] public bool Nexus { get; set; }

    /// <summary>Nexus opinion rationale statement. (.20)</summary>
    [Id(19)] public string NexusRationale { get; set; } = string.Empty;

    /// <summary>List of DBQ IDs completed for this exam. (.21)</summary>
    [Id(20)] public List<string> DbqIds { get; set; } = new();

    /// <summary>Date/time this exam record was created. (.22)</summary>
    [Id(21)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time this exam record was last modified. (.23)</summary>
    [Id(22)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>User who created this exam record. (.24)</summary>
    [Id(23)] public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>Summary entry stored in the per-patient exam index.</summary>
[GenerateSerializer]
public class CPExamIndexEntry
{
    /// <summary>Unique exam identifier.</summary>
    [Id(0)] public string ExamId { get; set; } = string.Empty;

    /// <summary>Type of C&amp;P examination.</summary>
    [Id(1)] public CPExamType ExamType { get; set; }

    /// <summary>Current workflow status.</summary>
    [Id(2)] public CPExamStatus Status { get; set; }

    /// <summary>Scheduled exam date.</summary>
    [Id(3)] public DateTime ScheduledDate { get; set; }

    /// <summary>Completed date (null if not complete).</summary>
    [Id(4)] public DateTime? CompletedDate { get; set; }

    /// <summary>Examiner name.</summary>
    [Id(5)] public string ExaminerName { get; set; } = string.Empty;

    /// <summary>VA claim number.</summary>
    [Id(6)] public string ClaimNumber { get; set; } = string.Empty;

    /// <summary>Number of disability conditions claimed.</summary>
    [Id(7)] public int DisabilityCount { get; set; }

    /// <summary>Number of DBQs completed for this exam.</summary>
    [Id(8)] public int DbqCount { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────
// DBQState — Disability Benefits Questionnaire
// ──────────────────────────────────────────────────────────────────────────

/// <summary>State of a single Disability Benefits Questionnaire (DBQ) document.</summary>
[GenerateSerializer]
public class DBQState
{
    /// <summary>Unique DBQ identifier. (.01) CP-DBQ:{guid}</summary>
    [Id(0)] public string DbqId { get; set; } = string.Empty;

    /// <summary>Linked C&amp;P exam identifier. (.02)</summary>
    [Id(1)] public string ExamId { get; set; } = string.Empty;

    /// <summary>Patient DFN / ICN. (.03)</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name. (.04)</summary>
    [Id(3)] public string PatientName { get; set; } = string.Empty;

    /// <summary>DBQ form type / category. (.05)</summary>
    [Id(4)] public DBQType DbqType { get; set; } = DBQType.GeneralMedical;

    /// <summary>VA form number (e.g., 21-0960A-1). (.06)</summary>
    [Id(5)] public string DbqFormNumber { get; set; } = string.Empty;

    /// <summary>Descriptive title of the DBQ. (.07)</summary>
    [Id(6)] public string DbqTitle { get; set; } = string.Empty;

    /// <summary>VA claim number. (.08)</summary>
    [Id(7)] public string ClaimNumber { get; set; } = string.Empty;

    /// <summary>Plain-text description of the condition being claimed. (.09)</summary>
    [Id(8)] public string ConditionClaimed { get; set; } = string.Empty;

    /// <summary>ICD-10 diagnosis code for the claimed condition. (.10)</summary>
    [Id(9)] public string DiagnosisCode { get; set; } = string.Empty;

    /// <summary>Full text of the diagnosis. (.11)</summary>
    [Id(10)] public string DiagnosisDescription { get; set; } = string.Empty;

    /// <summary>Medical history section narrative. (.12)</summary>
    [Id(11)] public string HistorySection { get; set; } = string.Empty;

    /// <summary>Symptoms and findings section. (.13)</summary>
    [Id(12)] public string SymptomsSection { get; set; } = string.Empty;

    /// <summary>Functional impact on occupational and daily activities. (.14)</summary>
    [Id(13)] public string FunctionalImpactSection { get; set; } = string.Empty;

    /// <summary>Range of motion measurements and findings. (.15)</summary>
    [Id(14)] public string RangeOfMotionSection { get; set; } = string.Empty;

    /// <summary>Mental status examination findings (for mental health DBQs). (.16)</summary>
    [Id(15)] public string MentalStatusSection { get; set; } = string.Empty;

    /// <summary>Results of diagnostic tests (labs, imaging, etc.). (.17)</summary>
    [Id(16)] public string DiagnosticTestsSection { get; set; } = string.Empty;

    /// <summary>Examiner opinions and rationale section. (.18)</summary>
    [Id(17)] public string OpinionsSection { get; set; } = string.Empty;

    /// <summary>Whether examiner opined nexus between service and condition. (.19)</summary>
    [Id(18)] public bool NexusOpinion { get; set; }

    /// <summary>Nexus opinion statement text. (.20)</summary>
    [Id(19)] public string NexusStatement { get; set; } = string.Empty;

    /// <summary>Service connection basis recommended by examiner. (.21)</summary>
    [Id(20)] public ServiceConnectionType ServiceConnectionType { get; set; } = ServiceConnectionType.Pending;

    /// <summary>Whether residuals are considered permanent and total. (.22)</summary>
    [Id(21)] public bool ResidualsPermanent { get; set; }

    /// <summary>Whether examiner expects improvement of condition. (.23)</summary>
    [Id(22)] public bool ExpectedImprovement { get; set; }

    /// <summary>Proposed combined disability rating percentage (0–100). (.24)</summary>
    [Id(23)] public int ProposedRating { get; set; }

    /// <summary>Current workflow status of the DBQ. (.25)</summary>
    [Id(24)] public DBQStatus Status { get; set; } = DBQStatus.Draft;

    /// <summary>Date/time the DBQ was marked complete. (.26)</summary>
    [Id(25)] public DateTime? CompletedDate { get; set; }

    /// <summary>Date/time the DBQ was electronically signed. (.27)</summary>
    [Id(26)] public DateTime? SignedDate { get; set; }

    /// <summary>Name of the examiner who signed the DBQ. (.28)</summary>
    [Id(27)] public string SignedBy { get; set; } = string.Empty;

    /// <summary>Date/time this DBQ record was created. (.29)</summary>
    [Id(28)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time this DBQ record was last modified. (.30)</summary>
    [Id(29)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient DBQ index.</summary>
[GenerateSerializer]
public class DBQIndexEntry
{
    /// <summary>Unique DBQ identifier.</summary>
    [Id(0)] public string DbqId { get; set; } = string.Empty;

    /// <summary>Linked exam identifier.</summary>
    [Id(1)] public string ExamId { get; set; } = string.Empty;

    /// <summary>DBQ form type.</summary>
    [Id(2)] public DBQType DbqType { get; set; }

    /// <summary>Descriptive DBQ title.</summary>
    [Id(3)] public string DbqTitle { get; set; } = string.Empty;

    /// <summary>Condition being claimed.</summary>
    [Id(4)] public string ConditionClaimed { get; set; } = string.Empty;

    /// <summary>Current workflow status.</summary>
    [Id(5)] public DBQStatus Status { get; set; }

    /// <summary>Proposed disability rating percentage.</summary>
    [Id(6)] public int ProposedRating { get; set; }

    /// <summary>Service connection recommendation.</summary>
    [Id(7)] public ServiceConnectionType ServiceConnectionType { get; set; }

    /// <summary>Date DBQ was completed (null if still draft).</summary>
    [Id(8)] public DateTime? CompletedDate { get; set; }
}
