// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Type of patient complaint or inquiry. VistA File #745 (.06)</summary>
public enum ComplaintType
{
    Informal,
    Formal,
    Grievance,
    CongressionalInquiry,
    OIGReferral,
    MediaInquiry,
    LegalAction,
    Other
}

/// <summary>Category/subject area of the complaint. VistA File #745 (.07)</summary>
public enum ComplaintCategory
{
    AccessToCare,
    QualityOfCare,
    StaffConduct,
    Billing,
    FacilityEnvironment,
    PrivacyBreach,
    BenefitsDetermination,
    WaitTime,
    Communication,
    Medication,
    PatientRights,
    Other
}

/// <summary>Current complaint status. VistA File #745 (.09)</summary>
public enum ComplaintStatus
{
    Received,
    Acknowledged,
    UnderInvestigation,
    ResponseDrafted,
    Resolved,
    Closed,
    Withdrawn,
    Escalated
}

/// <summary>Urgency level of complaint. VistA File #745 (.08)</summary>
public enum ComplaintPriority
{
    Routine,
    Urgent,
    Immediate
}

/// <summary>Source or originator of the complaint. VistA File #745 (.10)</summary>
public enum InquirySource
{
    PatientSelf,
    FamilyMember,
    Attorney,
    CongressionalOffice,
    MediaOutlet,
    OIG,
    StateAgency,
    Internal,
    Other
}

/// <summary>Determination outcome of complaint resolution. VistA File #745 (.21)</summary>
public enum ResolutionOutcome
{
    Substantiated,
    Unsubstantiated,
    PartiallySubstantiated,
    NoActionRequired,
    Referred,
    Withdrawn,
    CouldNotDetermine
}

/// <summary>Type of Congressional office submitting the inquiry.</summary>
public enum CongressionalInquiryType
{
    HouseOfRepresentatives,
    Senate,
    StateLegislature,
    Other
}

/// <summary>A single correspondence entry in the complaint log.</summary>
[GenerateSerializer]
public class ComplaintCorrespondence
{
    /// <summary>Unique identifier for this correspondence entry.</summary>
    [Id(0)] public string CorrespondenceId { get; set; } = string.Empty;

    /// <summary>Date and time of correspondence.</summary>
    [Id(1)] public DateTime Date { get; set; }

    /// <summary>Direction: Inbound or Outbound.</summary>
    [Id(2)] public string Direction { get; set; } = string.Empty;

    /// <summary>Method: Phone, Letter, Email, InPerson, Fax.</summary>
    [Id(3)] public string Method { get; set; } = string.Empty;

    /// <summary>Brief summary of the communication.</summary>
    [Id(4)] public string Summary { get; set; } = string.Empty;

    /// <summary>Staff member who handled this correspondence.</summary>
    [Id(5)] public string HandledBy { get; set; } = string.Empty;
}

/// <summary>
/// Patient complaint or grievance record.
/// VistA File #745 (PATIENT REPRESENTATIVE). PATREPE.m
/// </summary>
[GenerateSerializer]
public class ComplaintState
{
    /// <summary>Unique complaint identifier. VistA File #745 (.01)</summary>
    [Id(0)] public string ComplaintId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA File #745 (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name. VistA File #745 (.03)</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date the complaint event occurred. VistA File #745 (.04)</summary>
    [Id(3)] public DateTime ComplaintDate { get; set; }

    /// <summary>Date complaint was received by Patient Advocate. VistA File #745 (.05)</summary>
    [Id(4)] public DateTime ReceivedDate { get; set; }

    /// <summary>Type of complaint or inquiry. VistA File #745 (.06)</summary>
    [Id(5)] public ComplaintType ComplaintType { get; set; }

    /// <summary>Category of the complaint. VistA File #745 (.07)</summary>
    [Id(6)] public ComplaintCategory Category { get; set; }

    /// <summary>Priority/urgency level. VistA File #745 (.08)</summary>
    [Id(7)] public ComplaintPriority Priority { get; set; }

    /// <summary>Current status. VistA File #745 (.09)</summary>
    [Id(8)] public ComplaintStatus Status { get; set; }

    /// <summary>Source of the complaint. VistA File #745 (.10)</summary>
    [Id(9)] public InquirySource Source { get; set; }

    /// <summary>Detailed description of the complaint. VistA File #745 (.11)</summary>
    [Id(10)] public string NarrativeDescription { get; set; } = string.Empty;

    /// <summary>Specific concern or desired resolution outcome. VistA File #745 (.12)</summary>
    [Id(11)] public string SpecificConcern { get; set; } = string.Empty;

    /// <summary>Department or service involved. VistA File #745 (.13)</summary>
    [Id(12)] public string DepartmentInvolved { get; set; } = string.Empty;

    /// <summary>Assigned patient advocate ID. VistA File #745 (.14)</summary>
    [Id(13)] public string AssignedAdvocateId { get; set; } = string.Empty;

    /// <summary>Assigned patient advocate name. VistA File #745 (.15)</summary>
    [Id(14)] public string AssignedAdvocateName { get; set; } = string.Empty;

    /// <summary>Date complaint was acknowledged to patient/reporter. VistA File #745 (.16)</summary>
    [Id(15)] public DateTime? AcknowledgmentDate { get; set; }

    /// <summary>Acknowledgment due date. VistA File #745 (.17)</summary>
    [Id(16)] public DateTime? AcknowledgmentDue { get; set; }

    /// <summary>Resolution/response due date. VistA File #745 (.18)</summary>
    [Id(17)] public DateTime? ResponseDue { get; set; }

    /// <summary>Date complaint was resolved. VistA File #745 (.19)</summary>
    [Id(18)] public DateTime? ResolvedDate { get; set; }

    /// <summary>Date complaint was closed. VistA File #745 (.20)</summary>
    [Id(19)] public DateTime? ClosedDate { get; set; }

    /// <summary>Resolution determination outcome. VistA File #745 (.21)</summary>
    [Id(20)] public ResolutionOutcome? Outcome { get; set; }

    /// <summary>Summary of resolution and actions taken. VistA File #745 (.22)</summary>
    [Id(21)] public string ResolutionSummary { get; set; } = string.Empty;

    /// <summary>Actions taken to address the complaint. VistA File #745 (.23)</summary>
    [Id(22)] public List<string> ActionsTaken { get; set; } = new();

    /// <summary>Correspondence log entries. VistA File #745 (.24)</summary>
    [Id(23)] public List<ComplaintCorrespondence> CorrespondenceLog { get; set; } = new();

    /// <summary>Name of the person filing on the patient's behalf. VistA File #745 (.25)</summary>
    [Id(24)] public string ReporterName { get; set; } = string.Empty;

    /// <summary>Relationship of reporter to patient. VistA File #745 (.26)</summary>
    [Id(25)] public string ReporterRelationship { get; set; } = string.Empty;

    /// <summary>Whether complaint is confidential. VistA File #745 (.27)</summary>
    [Id(26)] public bool IsConfidential { get; set; }

    /// <summary>Staff member who entered/created the complaint. VistA File #745 (.28)</summary>
    [Id(27)] public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(28)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for complaint index queries.</summary>
[GenerateSerializer]
public class ComplaintIndexEntry
{
    [Id(0)] public string ComplaintId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime ReceivedDate { get; set; }
    [Id(4)] public ComplaintType ComplaintType { get; set; }
    [Id(5)] public ComplaintCategory Category { get; set; }
    [Id(6)] public ComplaintPriority Priority { get; set; }
    [Id(7)] public ComplaintStatus Status { get; set; }
    [Id(8)] public string AssignedAdvocateName { get; set; } = string.Empty;
    [Id(9)] public DateTime? ResponseDue { get; set; }
}

/// <summary>
/// Congressional inquiry record with federal response timeline tracking.
/// Strict federal requirements: 7-day acknowledgment, 20-day full response.
/// </summary>
[GenerateSerializer]
public class CongressionalInquiryState
{
    /// <summary>Unique inquiry identifier.</summary>
    [Id(0)] public string InquiryId { get; set; } = string.Empty;

    /// <summary>Patient the inquiry concerns.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date inquiry was received.</summary>
    [Id(3)] public DateTime ReceivedDate { get; set; }

    /// <summary>Type of Congressional office (House, Senate, State).</summary>
    [Id(4)] public CongressionalInquiryType InquiryType { get; set; }

    /// <summary>Name of Congressional office or representative/senator.</summary>
    [Id(5)] public string CongressionalOfficeName { get; set; } = string.Empty;

    /// <summary>Contact person at the Congressional office.</summary>
    [Id(6)] public string CongressionalContactName { get; set; } = string.Empty;

    /// <summary>Congressional office phone number.</summary>
    [Id(7)] public string CongressionalPhone { get; set; } = string.Empty;

    /// <summary>Congressional office email or fax.</summary>
    [Id(8)] public string CongressionalEmail { get; set; } = string.Empty;

    /// <summary>Subject or issue of the inquiry.</summary>
    [Id(9)] public string Subject { get; set; } = string.Empty;

    /// <summary>Full text of the inquiry.</summary>
    [Id(10)] public string InquiryText { get; set; } = string.Empty;

    /// <summary>Assigned handler/advocate ID.</summary>
    [Id(11)] public string AssignedHandlerId { get; set; } = string.Empty;

    /// <summary>Assigned handler name.</summary>
    [Id(12)] public string AssignedHandlerName { get; set; } = string.Empty;

    /// <summary>Current status.</summary>
    [Id(13)] public ComplaintStatus Status { get; set; }

    /// <summary>7-day federal acknowledgment due date.</summary>
    [Id(14)] public DateTime AcknowledgmentDue { get; set; }

    /// <summary>Date acknowledged to congressional office.</summary>
    [Id(15)] public DateTime? AcknowledgmentDate { get; set; }

    /// <summary>20-day federal full response due date.</summary>
    [Id(16)] public DateTime ResponseDue { get; set; }

    /// <summary>Date of interim response, if provided.</summary>
    [Id(17)] public DateTime? InterimResponseDate { get; set; }

    /// <summary>Interim response text.</summary>
    [Id(18)] public string InterimResponseText { get; set; } = string.Empty;

    /// <summary>Final response text sent to congressional office.</summary>
    [Id(19)] public string FinalResponseText { get; set; } = string.Empty;

    /// <summary>Date final response was sent.</summary>
    [Id(20)] public DateTime? FinalResponseDate { get; set; }

    /// <summary>Resolution outcome.</summary>
    [Id(21)] public ResolutionOutcome? Outcome { get; set; }

    /// <summary>Associated complaint ID if linked to a formal complaint.</summary>
    [Id(22)] public string LinkedComplaintId { get; set; } = string.Empty;

    /// <summary>Staff member who created the inquiry record.</summary>
    [Id(23)] public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(24)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for Congressional inquiry index queries.</summary>
[GenerateSerializer]
public class CongressionalInquiryIndexEntry
{
    [Id(0)] public string InquiryId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime ReceivedDate { get; set; }
    [Id(4)] public CongressionalInquiryType InquiryType { get; set; }
    [Id(5)] public string CongressionalOfficeName { get; set; } = string.Empty;
    [Id(6)] public ComplaintStatus Status { get; set; }
    [Id(7)] public DateTime AcknowledgmentDue { get; set; }
    [Id(8)] public DateTime ResponseDue { get; set; }
    [Id(9)] public bool IsAcknowledgmentOverdue { get; set; }
    [Id(10)] public bool IsResponseOverdue { get; set; }
}
