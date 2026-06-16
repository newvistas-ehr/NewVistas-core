// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lab EDI Order grain. Represents an electronic lab order
/// sent to an external reference laboratory for send-out testing.
/// </summary>
[GenerateSerializer]
public class LabEdiOrderState
{
    /// <summary>
    /// Unique order identifier (grain key)
    /// </summary>
    [Id(0)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (ICN or DFN)
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient display name
    /// </summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Reference laboratory name
    /// </summary>
    [Id(3)]
    public string ReferenceLab { get; set; } = string.Empty;

    /// <summary>
    /// Reference laboratory identifier (maps to config grain key)
    /// </summary>
    [Id(4)]
    public string ReferenceLabId { get; set; } = string.Empty;

    /// <summary>
    /// Ordering provider's identifier (DUZ or NPI)
    /// </summary>
    [Id(5)]
    public string OrderingProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Ordering provider's display name
    /// </summary>
    [Id(6)]
    public string OrderingProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Tests requested for this order
    /// </summary>
    [Id(7)]
    public List<LabEdiTestRequest> TestsRequested { get; set; } = new();

    /// <summary>
    /// Clinical history / reason for test
    /// </summary>
    [Id(8)]
    public string? ClinicalHistory { get; set; }

    /// <summary>
    /// Specimen type (e.g., "BLOOD", "URINE", "SERUM")
    /// </summary>
    [Id(9)]
    public string? SpecimenType { get; set; }

    /// <summary>
    /// Specimen accession/container identifier
    /// </summary>
    [Id(10)]
    public string? SpecimenId { get; set; }

    /// <summary>
    /// Date/time of specimen collection
    /// </summary>
    [Id(11)]
    public DateTime? CollectionDate { get; set; }

    /// <summary>
    /// Order priority (e.g., "ROUTINE", "STAT", "ASAP")
    /// </summary>
    [Id(12)]
    public string? Priority { get; set; }

    /// <summary>
    /// Current order status
    /// </summary>
    [Id(13)]
    public LabEdiOrderStatus Status { get; set; } = LabEdiOrderStatus.Draft;

    /// <summary>
    /// HL7 message control ID from transmitted ORM message
    /// </summary>
    [Id(14)]
    public string? Hl7MessageId { get; set; }

    /// <summary>
    /// Date/time order was transmitted to reference lab
    /// </summary>
    [Id(15)]
    public DateTime? TransmittedDate { get; set; }

    /// <summary>
    /// Date/time acknowledgment was received from reference lab
    /// </summary>
    [Id(16)]
    public DateTime? AcknowledgedDate { get; set; }

    /// <summary>
    /// Acknowledgment code from reference lab (AA, AE, AR)
    /// </summary>
    [Id(17)]
    public string? AcknowledgmentCode { get; set; }

    /// <summary>
    /// Date/time results were received from reference lab
    /// </summary>
    [Id(18)]
    public DateTime? ResultReceivedDate { get; set; }

    /// <summary>
    /// Reason for rejection by reference lab
    /// </summary>
    [Id(19)]
    public string? RejectedReason { get; set; }

    /// <summary>
    /// Date/time order was rejected
    /// </summary>
    [Id(20)]
    public DateTime? RejectedDate { get; set; }

    /// <summary>
    /// User who cancelled the order
    /// </summary>
    [Id(21)]
    public string? CancelledBy { get; set; }

    /// <summary>
    /// Reason for cancellation
    /// </summary>
    [Id(22)]
    public string? CancelledReason { get; set; }

    /// <summary>
    /// Date/time the order was created
    /// </summary>
    [Id(23)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date/time the order was last modified
    /// </summary>
    [Id(24)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A test requested as part of an EDI lab order.
/// </summary>
[GenerateSerializer]
public class LabEdiTestRequest
{
    /// <summary>
    /// Local test code
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
    /// Specimen requirements for this test
    /// </summary>
    [Id(3)]
    public string? SpecimenRequirements { get; set; }
}

/// <summary>
/// Order status lifecycle for EDI lab orders.
/// </summary>
[GenerateSerializer]
public enum LabEdiOrderStatus
{
    /// <summary>
    /// Order created but not yet transmitted
    /// </summary>
    Draft,

    /// <summary>
    /// Order transmitted to reference lab via HL7
    /// </summary>
    Transmitted,

    /// <summary>
    /// Acknowledgment received from reference lab
    /// </summary>
    Acknowledged,

    /// <summary>
    /// Results received from reference lab
    /// </summary>
    ResultReceived,

    /// <summary>
    /// Order rejected by reference lab
    /// </summary>
    Rejected,

    /// <summary>
    /// Order cancelled by ordering facility
    /// </summary>
    Cancelled
}
