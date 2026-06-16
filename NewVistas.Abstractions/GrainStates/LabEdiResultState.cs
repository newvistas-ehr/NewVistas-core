// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lab EDI Result grain. Represents an inbound result set
/// received from an external reference laboratory via ORU^R01 message.
/// </summary>
[GenerateSerializer]
public class LabEdiResultState
{
    /// <summary>
    /// Unique result identifier (grain key)
    /// </summary>
    [Id(0)]
    public string ResultId { get; set; } = string.Empty;

    /// <summary>
    /// Associated outbound order identifier
    /// </summary>
    [Id(1)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (ICN or DFN)
    /// </summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Reference laboratory name
    /// </summary>
    [Id(3)]
    public string ReferenceLab { get; set; } = string.Empty;

    /// <summary>
    /// Reference laboratory identifier
    /// </summary>
    [Id(4)]
    public string ReferenceLabId { get; set; } = string.Empty;

    /// <summary>
    /// HL7 message control ID from the ORU message
    /// </summary>
    [Id(5)]
    public string Hl7MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Individual test results within this result set
    /// </summary>
    [Id(6)]
    public List<LabEdiResultItem> Results { get; set; } = new();

    /// <summary>
    /// Date/time results were produced by the reference lab
    /// </summary>
    [Id(7)]
    public DateTime ResultDate { get; set; }

    /// <summary>
    /// CLIA number of the performing laboratory
    /// </summary>
    [Id(8)]
    public string? PerformingLabClia { get; set; }

    /// <summary>
    /// Current result review status
    /// </summary>
    [Id(9)]
    public LabEdiResultStatus Status { get; set; } = LabEdiResultStatus.Received;

    /// <summary>
    /// User who reviewed the result
    /// </summary>
    [Id(10)]
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Display name of reviewer
    /// </summary>
    [Id(11)]
    public string? ReviewedByName { get; set; }

    /// <summary>
    /// Date/time result was reviewed
    /// </summary>
    [Id(12)]
    public DateTime? ReviewedDate { get; set; }

    /// <summary>
    /// Review comments
    /// </summary>
    [Id(13)]
    public string? ReviewComments { get; set; }

    /// <summary>
    /// User who accepted and filed the result
    /// </summary>
    [Id(14)]
    public string? AcceptedBy { get; set; }

    /// <summary>
    /// Date/time result was accepted
    /// </summary>
    [Id(15)]
    public DateTime? AcceptedDate { get; set; }

    /// <summary>
    /// User who rejected the result
    /// </summary>
    [Id(16)]
    public string? RejectedBy { get; set; }

    /// <summary>
    /// Reason for rejection
    /// </summary>
    [Id(17)]
    public string? RejectedReason { get; set; }

    /// <summary>
    /// Date/time result was created in the system
    /// </summary>
    [Id(18)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date/time result was last modified
    /// </summary>
    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An individual test result within an EDI result set.
/// Maps to an OBX segment in the HL7 ORU^R01 message.
/// </summary>
[GenerateSerializer]
public class LabEdiResultItem
{
    /// <summary>
    /// Test code from the reference lab
    /// </summary>
    [Id(0)]
    public string TestCode { get; set; } = string.Empty;

    /// <summary>
    /// Test display name
    /// </summary>
    [Id(1)]
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// LOINC code for the test (if mapped)
    /// </summary>
    [Id(2)]
    public string? LoincCode { get; set; }

    /// <summary>
    /// Result value (numeric or text)
    /// </summary>
    [Id(3)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Units of measurement (e.g., "mg/dL", "mmol/L")
    /// </summary>
    [Id(4)]
    public string? Units { get; set; }

    /// <summary>
    /// Reference range (e.g., "70-100", "3.5-5.0")
    /// </summary>
    [Id(5)]
    public string? ReferenceRange { get; set; }

    /// <summary>
    /// Abnormal flag: H=High, L=Low, HH=Critical High, LL=Critical Low, A=Abnormal, N=Normal
    /// Maps to OBX-8 in HL7.
    /// </summary>
    [Id(6)]
    public string? AbnormalFlag { get; set; }

    /// <summary>
    /// Result status: F=Final, P=Preliminary, C=Correction
    /// Maps to OBX-11 in HL7.
    /// </summary>
    [Id(7)]
    public string ResultStatus { get; set; } = "F";
}

/// <summary>
/// Result review status lifecycle for EDI lab results.
/// </summary>
[GenerateSerializer]
public enum LabEdiResultStatus
{
    /// <summary>
    /// Result received from reference lab, awaiting review
    /// </summary>
    Received,

    /// <summary>
    /// Result reviewed by lab professional
    /// </summary>
    Reviewed,

    /// <summary>
    /// Result accepted and filed to patient record
    /// </summary>
    Accepted,

    /// <summary>
    /// Result rejected — will not be filed
    /// </summary>
    Rejected
}
