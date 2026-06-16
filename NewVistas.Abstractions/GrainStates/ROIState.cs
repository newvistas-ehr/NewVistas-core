// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Current status of a records release request. VistA File #195 (.02)</summary>
public enum ROIRequestStatus
{
    Received,
    Acknowledged,
    InProcess,
    PendingAuthorization,
    Fulfilled,
    Denied,
    Cancelled,
    Withdrawn
}

/// <summary>Type of records being requested. VistA File #195 (.03)</summary>
public enum ROIRequestType
{
    MedicalRecords,
    BillingRecords,
    ImagingRecords,
    LabRecords,
    MentalHealthRecords,
    SubstanceAbuseRecords,
    HIVRecords,
    WholeRecord,
    Other
}

/// <summary>Type of entity requesting the records. VistA File #195 (.04)</summary>
public enum RequesterType
{
    Patient,
    PatientRepresentative,
    Attorney,
    InsuranceCompany,
    GovernmentAgency,
    LawEnforcement,
    ResearchInstitution,
    HealthcareProvider,
    Other
}

/// <summary>Status of the authorization form. VistA File #195 (.05)</summary>
public enum AuthorizationStatus
{
    NotRequired,
    Received,
    Pending,
    Expired,
    Revoked,
    Deficient
}

/// <summary>Method used to fulfill the records request. VistA File #195 (.06)</summary>
public enum FulfillmentMethod
{
    Mail,
    Fax,
    PickUp,
    ElectronicTransfer,
    Portal,
    Email,
    CD_DVD,
    Other
}

/// <summary>Priority level of the records request.</summary>
public enum ROIRequestPriority
{
    Routine,
    Expedited,
    Urgent
}

/// <summary>
/// HIPAA disclosure type. TPO disclosures (Treatment, Payment, HealthcareOperations)
/// are NOT subject to the accounting of disclosures requirement.
/// All others ARE subject to accounting per 45 CFR 164.528.
/// </summary>
public enum HIPAADisclosureType
{
    // TPO — NOT subject to accounting
    Treatment,
    Payment,
    HealthcareOperations,
    // Required/Permitted — subject to accounting
    PublicHealth,
    HealthOversight,
    LawEnforcement,
    JudicialProceeding,
    CoronersOffice,
    WorkersCompensation,
    ResearchWithWaiver,
    NationalSecurity,
    Correctional,
    // Authorized — subject to accounting
    PatientAuthorization,
    // Other facility-wide disclosure
    Other
}

/// <summary>
/// Record request for release of patient information.
/// VistA File #195 (RELEASE OF INFORMATION). ROIS.m, ROI.m
/// </summary>
[GenerateSerializer]
public class ROIRequestState
{
    /// <summary>Unique request identifier. VistA File #195 (.01)</summary>
    [Id(0)] public string RequestId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA File #195 (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name. VistA File #195 (.03)</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient date of birth for identity verification. VistA File #195 (.04)</summary>
    [Id(3)] public DateTime? PatientDOB { get; set; }

    /// <summary>Date request was received. VistA File #195 (.05)</summary>
    [Id(4)] public DateTime ReceivedDate { get; set; }

    /// <summary>Type of records requested. VistA File #195 (.06)</summary>
    [Id(5)] public ROIRequestType RequestType { get; set; }

    /// <summary>Type of requester. VistA File #195 (.07)</summary>
    [Id(6)] public RequesterType RequesterType { get; set; }

    /// <summary>Name of the requester. VistA File #195 (.08)</summary>
    [Id(7)] public string RequesterName { get; set; } = string.Empty;

    /// <summary>Organization of the requester. VistA File #195 (.09)</summary>
    [Id(8)] public string RequesterOrganization { get; set; } = string.Empty;

    /// <summary>Requester mailing address. VistA File #195 (.10)</summary>
    [Id(9)] public string RequesterAddress { get; set; } = string.Empty;

    /// <summary>Requester phone number. VistA File #195 (.11)</summary>
    [Id(10)] public string RequesterPhone { get; set; } = string.Empty;

    /// <summary>Requester fax number. VistA File #195 (.12)</summary>
    [Id(11)] public string RequesterFax { get; set; } = string.Empty;

    /// <summary>Requester email address. VistA File #195 (.13)</summary>
    [Id(12)] public string RequesterEmail { get; set; } = string.Empty;

    /// <summary>Stated purpose of the request. VistA File #195 (.14)</summary>
    [Id(13)] public string PurposeOfRequest { get; set; } = string.Empty;

    /// <summary>Specific records or types of information requested. VistA File #195 (.15)</summary>
    [Id(14)] public List<string> RecordsRequested { get; set; } = new();

    /// <summary>Start date of the requested record date range. VistA File #195 (.16)</summary>
    [Id(15)] public DateTime? DateRangeStart { get; set; }

    /// <summary>End date of the requested record date range. VistA File #195 (.17)</summary>
    [Id(16)] public DateTime? DateRangeEnd { get; set; }

    /// <summary>Authorization form status. VistA File #195 (.18)</summary>
    [Id(17)] public AuthorizationStatus AuthorizationStatus { get; set; }

    /// <summary>Date authorization was received. VistA File #195 (.19)</summary>
    [Id(18)] public DateTime? AuthorizationDate { get; set; }

    /// <summary>Authorization expiration date. VistA File #195 (.20)</summary>
    [Id(19)] public DateTime? AuthorizationExpirationDate { get; set; }

    /// <summary>Priority of the request. VistA File #195 (.21)</summary>
    [Id(20)] public ROIRequestPriority Priority { get; set; }

    /// <summary>Current request status. VistA File #195 (.22)</summary>
    [Id(21)] public ROIRequestStatus Status { get; set; }

    /// <summary>HIPAA-mandated due date (30 days from receipt). VistA File #195 (.23)</summary>
    [Id(22)] public DateTime DueDate { get; set; }

    /// <summary>Assigned ROI staff member ID. VistA File #195 (.24)</summary>
    [Id(23)] public string AssignedStaffId { get; set; } = string.Empty;

    /// <summary>Assigned ROI staff member name. VistA File #195 (.25)</summary>
    [Id(24)] public string AssignedStaffName { get; set; } = string.Empty;

    /// <summary>Internal processing notes. VistA File #195 (.26)</summary>
    [Id(25)] public string ProcessingNotes { get; set; } = string.Empty;

    /// <summary>Date records were fulfilled/released. VistA File #195 (.27)</summary>
    [Id(26)] public DateTime? FulfillmentDate { get; set; }

    /// <summary>Method used to release the records. VistA File #195 (.28)</summary>
    [Id(27)] public FulfillmentMethod FulfillmentMethod { get; set; }

    /// <summary>Notes on fulfillment (tracking number, etc.). VistA File #195 (.29)</summary>
    [Id(28)] public string FulfillmentNotes { get; set; } = string.Empty;

    /// <summary>Reason for denial if request was denied. VistA File #195 (.30)</summary>
    [Id(29)] public string DenialReason { get; set; } = string.Empty;

    /// <summary>Number of pages released. VistA File #195 (.31)</summary>
    [Id(30)] public int NumberOfPagesFulfilled { get; set; }

    /// <summary>Fee charged for copying/processing. VistA File #195 (.32)</summary>
    [Id(31)] public decimal FeeCharged { get; set; }

    /// <summary>Staff member who created the request. VistA File #195 (.33)</summary>
    [Id(32)] public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(33)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for ROI request index queries.</summary>
[GenerateSerializer]
public class ROIRequestIndexEntry
{
    [Id(0)] public string RequestId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime ReceivedDate { get; set; }
    [Id(4)] public ROIRequestType RequestType { get; set; }
    [Id(5)] public RequesterType RequesterType { get; set; }
    [Id(6)] public string RequesterName { get; set; } = string.Empty;
    [Id(7)] public ROIRequestStatus Status { get; set; }
    [Id(8)] public DateTime DueDate { get; set; }
    [Id(9)] public string AssignedStaffName { get; set; } = string.Empty;
    [Id(10)] public ROIRequestPriority Priority { get; set; }
}

/// <summary>
/// HIPAA disclosure record for accounting of disclosures.
/// 45 CFR 164.528 requires accounting of disclosures made in the past 6 years.
/// VistA File #195.1 (ROI ACCOUNTING OF DISCLOSURES).
/// </summary>
[GenerateSerializer]
public class HIPAADisclosureState
{
    /// <summary>Unique disclosure identifier.</summary>
    [Id(0)] public string DisclosureId { get; set; } = string.Empty;

    /// <summary>Patient whose information was disclosed.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date and time of disclosure.</summary>
    [Id(3)] public DateTime DisclosureDate { get; set; }

    /// <summary>Type/basis of disclosure.</summary>
    [Id(4)] public HIPAADisclosureType DisclosureType { get; set; }

    /// <summary>Name of recipient individual or organization.</summary>
    [Id(5)] public string RecipientName { get; set; } = string.Empty;

    /// <summary>Organization the recipient represents.</summary>
    [Id(6)] public string RecipientOrganization { get; set; } = string.Empty;

    /// <summary>Address of the recipient.</summary>
    [Id(7)] public string RecipientAddress { get; set; } = string.Empty;

    /// <summary>Stated purpose for the disclosure.</summary>
    [Id(8)] public string PurposeOfDisclosure { get; set; } = string.Empty;

    /// <summary>Description of the information that was disclosed.</summary>
    [Id(9)] public string InformationDisclosed { get; set; } = string.Empty;

    /// <summary>Date range of the information that was disclosed.</summary>
    [Id(10)] public string DateRangeOfInformation { get; set; } = string.Empty;

    /// <summary>Number of pages or records disclosed.</summary>
    [Id(11)] public int NumberOfPages { get; set; }

    /// <summary>
    /// Whether this disclosure is subject to accounting per 45 CFR 164.528.
    /// TPO disclosures (Treatment, Payment, HealthcareOperations) are excluded.
    /// </summary>
    [Id(12)] public bool IsSubjectToAccounting { get; set; }

    /// <summary>Whether a valid patient authorization was received.</summary>
    [Id(13)] public bool AuthorizationReceived { get; set; }

    /// <summary>Linked ROI request ID, if disclosure resulted from a record request.</summary>
    [Id(14)] public string LinkedRequestId { get; set; } = string.Empty;

    /// <summary>Staff member who made or authorized the disclosure.</summary>
    [Id(15)] public string DisclosedBy { get; set; } = string.Empty;

    /// <summary>Title/role of the disclosing staff member.</summary>
    [Id(16)] public string DisclosedByTitle { get; set; } = string.Empty;

    /// <summary>Timestamp when the record was created.</summary>
    [Id(17)] public DateTime CreatedDate { get; set; }
}

/// <summary>Summary entry for HIPAA disclosure index queries.</summary>
[GenerateSerializer]
public class HIPAADisclosureIndexEntry
{
    [Id(0)] public string DisclosureId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime DisclosureDate { get; set; }
    [Id(4)] public HIPAADisclosureType DisclosureType { get; set; }
    [Id(5)] public string RecipientName { get; set; } = string.Empty;
    [Id(6)] public string PurposeOfDisclosure { get; set; } = string.Empty;
    [Id(7)] public bool IsSubjectToAccounting { get; set; }
    [Id(8)] public string LinkedRequestId { get; set; } = string.Empty;
}
