// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Status of a research study.</summary>
[GenerateSerializer]
public enum IrbStudyStatus
{
    Draft,
    OpenForEnrollment,
    ClosedToEnrollment,
    Suspended,
    Completed,
    Withdrawn
}

/// <summary>Type of research study.</summary>
[GenerateSerializer]
public enum IrbStudyType
{
    Interventional,
    Observational,
    ExpandedAccess,
    Registry,
    Behavioral,
    DeviceStudy,
    Other
}

/// <summary>Clinical trial phase (VistA/FDA classification).</summary>
[GenerateSerializer]
public enum IrbStudyPhase
{
    NotApplicable,
    Phase1,
    Phase1And2,
    Phase2,
    Phase2And3,
    Phase3,
    Phase4
}

/// <summary>Type of IRB submission.</summary>
[GenerateSerializer]
public enum IrbSubmissionType
{
    InitialApplication,
    Amendment,
    ContinuingReview,
    SAEReport,
    ProtocolDeviation,
    Closure
}

/// <summary>Status of an IRB submission.</summary>
[GenerateSerializer]
public enum IrbSubmissionStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Disapproved,
    Tabled,
    Withdrawn,
    Expired
}

/// <summary>Research subject enrollment status.</summary>
[GenerateSerializer]
public enum SubjectEnrollmentStatus
{
    Screening,
    Enrolled,
    Active,
    Completed,
    Withdrawn,
    LostToFollowUp,
    Deceased
}

/// <summary>Type of informed consent obtained.</summary>
[GenerateSerializer]
public enum ConsentType
{
    Written,
    Oral,
    Waived,
    ChildAssent,
    LAR
}

/// <summary>Individual IRB submission record (stored within study state).</summary>
[GenerateSerializer]
public class IrbSubmissionEntry
{
    /// <summary>Unique identifier for this submission.</summary>
    [Id(0)] public string SubmissionId { get; set; } = string.Empty;

    /// <summary>Type of submission (initial, amendment, continuing review, etc.).</summary>
    [Id(1)] public IrbSubmissionType SubmissionType { get; set; }

    /// <summary>Date submitted to IRB.</summary>
    [Id(2)] public DateTime SubmissionDate { get; set; }

    /// <summary>Current status of the submission.</summary>
    [Id(3)] public IrbSubmissionStatus Status { get; set; } = IrbSubmissionStatus.Submitted;

    /// <summary>Date the IRB reviewed the submission.</summary>
    [Id(4)] public DateTime? ReviewDate { get; set; }

    /// <summary>IRB decision text (e.g., "Approved", "Approved with Modifications").</summary>
    [Id(5)] public string Decision { get; set; } = string.Empty;

    /// <summary>New approval expiration date (set on ContinuingReview approval).</summary>
    [Id(6)] public DateTime? NewExpirationDate { get; set; }

    /// <summary>Submission notes or description of changes.</summary>
    [Id(7)] public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Full state of a research study / IRB protocol. VistA Research Module (~File #900).
/// Grain key: "IRB-STUDY:{guid}".
/// </summary>
[GenerateSerializer]
public class ResearchStudyState
{
    /// <summary>Internal grain key / study ID (.01).</summary>
    [Id(0)] public string StudyId { get; set; } = string.Empty;

    /// <summary>IRB-assigned protocol number.</summary>
    [Id(1)] public string IrbProtocolNumber { get; set; } = string.Empty;

    /// <summary>Full title of the research study.</summary>
    [Id(2)] public string Title { get; set; } = string.Empty;

    /// <summary>Short/abbreviated study title.</summary>
    [Id(3)] public string ShortTitle { get; set; } = string.Empty;

    /// <summary>Name of the Principal Investigator.</summary>
    [Id(4)] public string PrincipalInvestigator { get; set; } = string.Empty;

    /// <summary>Employee/provider ID of the PI.</summary>
    [Id(5)] public string PiEmployeeId { get; set; } = string.Empty;

    /// <summary>Sponsor (company, NIH, VA, etc.).</summary>
    [Id(6)] public string Sponsor { get; set; } = string.Empty;

    /// <summary>Type of study (interventional, observational, etc.).</summary>
    [Id(7)] public IrbStudyType StudyType { get; set; }

    /// <summary>Clinical trial phase (NotApplicable for non-drug studies).</summary>
    [Id(8)] public IrbStudyPhase Phase { get; set; }

    /// <summary>Current status of the study.</summary>
    [Id(9)] public IrbStudyStatus Status { get; set; } = IrbStudyStatus.Draft;

    /// <summary>Target enrollment number.</summary>
    [Id(10)] public int TargetEnrollment { get; set; }

    /// <summary>Current actual enrollment count.</summary>
    [Id(11)] public int CurrentEnrollment { get; set; }

    /// <summary>Department or service conducting the study.</summary>
    [Id(12)] public string Department { get; set; } = string.Empty;

    /// <summary>Date of initial IRB approval.</summary>
    [Id(13)] public DateTime? InitialApprovalDate { get; set; }

    /// <summary>Current approval expiration date.</summary>
    [Id(14)] public DateTime? CurrentExpirationDate { get; set; }

    /// <summary>Due date for next continuing review submission.</summary>
    [Id(15)] public DateTime? NextContinuingReviewDue { get; set; }

    /// <summary>Study description and background.</summary>
    [Id(16)] public string Description { get; set; } = string.Empty;

    /// <summary>Study arms or cohorts (e.g., "Treatment A", "Placebo").</summary>
    [Id(17)] public List<string> StudyArms { get; set; } = new();

    /// <summary>All IRB submissions associated with this study.</summary>
    [Id(18)] public List<IrbSubmissionEntry> Submissions { get; set; } = new();

    /// <summary>Last modification timestamp (UTC).</summary>
    [Id(19)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Index entry for a research study, stored in the study index grain.</summary>
[GenerateSerializer]
public class IrbStudyIndexEntry
{
    /// <summary>Study grain key.</summary>
    [Id(0)] public string StudyId { get; set; } = string.Empty;

    /// <summary>IRB protocol number.</summary>
    [Id(1)] public string IrbProtocolNumber { get; set; } = string.Empty;

    /// <summary>Study title.</summary>
    [Id(2)] public string Title { get; set; } = string.Empty;

    /// <summary>Principal Investigator name.</summary>
    [Id(3)] public string PrincipalInvestigator { get; set; } = string.Empty;

    /// <summary>Study type.</summary>
    [Id(4)] public IrbStudyType StudyType { get; set; }

    /// <summary>Trial phase.</summary>
    [Id(5)] public IrbStudyPhase Phase { get; set; }

    /// <summary>Current study status.</summary>
    [Id(6)] public IrbStudyStatus Status { get; set; }

    /// <summary>Current enrollment count.</summary>
    [Id(7)] public int CurrentEnrollment { get; set; }

    /// <summary>Target enrollment.</summary>
    [Id(8)] public int TargetEnrollment { get; set; }

    /// <summary>Current IRB approval expiration date.</summary>
    [Id(9)] public DateTime? CurrentExpirationDate { get; set; }
}

/// <summary>
/// Full state of a research subject (enrolled patient). Grain key: "IRB-SUBJECT:{guid}".
/// </summary>
[GenerateSerializer]
public class ResearchSubjectState
{
    /// <summary>Unique subject ID (grain key).</summary>
    [Id(0)] public string SubjectId { get; set; } = string.Empty;

    /// <summary>Study the subject is enrolled in.</summary>
    [Id(1)] public string StudyId { get; set; } = string.Empty;

    /// <summary>Study title (denormalized for display).</summary>
    [Id(2)] public string StudyTitle { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(3)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(4)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient date of birth.</summary>
    [Id(5)] public DateTime? PatientDOB { get; set; }

    /// <summary>Date patient was screened for eligibility.</summary>
    [Id(6)] public DateTime ScreeningDate { get; set; }

    /// <summary>Date patient was formally enrolled.</summary>
    [Id(7)] public DateTime? EnrollmentDate { get; set; }

    /// <summary>Date informed consent was obtained.</summary>
    [Id(8)] public DateTime? ConsentDate { get; set; }

    /// <summary>Type of consent obtained.</summary>
    [Id(9)] public ConsentType ConsentType { get; set; }

    /// <summary>Name of person who obtained consent.</summary>
    [Id(10)] public string ConsentObtainedBy { get; set; } = string.Empty;

    /// <summary>Current enrollment status.</summary>
    [Id(11)] public SubjectEnrollmentStatus EnrollmentStatus { get; set; } = SubjectEnrollmentStatus.Screening;

    /// <summary>Study arm or cohort assignment.</summary>
    [Id(12)] public string Arm { get; set; } = string.Empty;

    /// <summary>Randomization code (for blinded trials).</summary>
    [Id(13)] public string? RandomizationCode { get; set; }

    /// <summary>Date of withdrawal (if withdrawn).</summary>
    [Id(14)] public DateTime? WithdrawalDate { get; set; }

    /// <summary>Reason for withdrawal.</summary>
    [Id(15)] public string WithdrawalReason { get; set; } = string.Empty;

    /// <summary>Date subject completed the study.</summary>
    [Id(16)] public DateTime? CompletionDate { get; set; }

    /// <summary>Clinical notes about the subject.</summary>
    [Id(17)] public string Notes { get; set; } = string.Empty;

    /// <summary>Last modification timestamp (UTC).</summary>
    [Id(18)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Index entry for a research subject within a specific study.</summary>
[GenerateSerializer]
public class ResearchSubjectIndexEntry
{
    /// <summary>Subject grain key.</summary>
    [Id(0)] public string SubjectId { get; set; } = string.Empty;

    /// <summary>Study grain key.</summary>
    [Id(1)] public string StudyId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(3)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Current enrollment status.</summary>
    [Id(4)] public SubjectEnrollmentStatus EnrollmentStatus { get; set; }

    /// <summary>Enrollment date.</summary>
    [Id(5)] public DateTime? EnrollmentDate { get; set; }

    /// <summary>Consent date.</summary>
    [Id(6)] public DateTime? ConsentDate { get; set; }

    /// <summary>Study arm assignment.</summary>
    [Id(7)] public string Arm { get; set; } = string.Empty;
}
