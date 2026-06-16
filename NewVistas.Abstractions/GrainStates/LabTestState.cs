// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Lab Test grain based on VistA LAB DATA file (#63) and LABORATORY TEST file (#60).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this lab test are persisted atomically with the
/// projection in a single <c>WriteStateAsync</c>.
/// </summary>

[GenerateSerializer]
public class LabTestState : EventSourcedStateBase
{
    /// <summary>
    /// Lab Test Internal Entry Number (IEN)
    /// </summary>
    [Id(0)]
    public string LabTestId { get; set; } = string.Empty;

    /// <summary>
    /// Patient IEN - Reference to PATIENT file (#2)
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Test IEN - Reference to LABORATORY TEST file (#60)
    /// </summary>
    [Id(2)]
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Test Name (e.g., WBC, RBC, HGB, HCT)
    /// </summary>
    [Id(3)]
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Test Code
    /// </summary>
    [Id(4)]
    public string? TestCode { get; set; }

    /// <summary>
    /// Order IEN - Reference to ORDER file (#100)
    /// </summary>
    [Id(5)]
    public string? OrderId { get; set; }

    /// <summary>
    /// Collection Date/Time
    /// </summary>
    [Id(6)]
    public DateTime? CollectionDateTime { get; set; }

    /// <summary>
    /// Specimen Type (e.g., Blood, Urine, Tissue)
    /// </summary>
    [Id(7)]
    public string? SpecimenType { get; set; }

    /// <summary>
    /// Collection Sample (e.g., LAVENDER, GRAY, MARBLED TOP)
    /// </summary>
    [Id(8)]
    public string? CollectionSample { get; set; }

    /// <summary>
    /// Result Date/Time
    /// </summary>
    [Id(9)]
    public DateTime? ResultDateTime { get; set; }

    /// <summary>
    /// Test Result Value
    /// </summary>
    [Id(10)]
    public string? ResultValue { get; set; }

    /// <summary>
    /// Result Unit (e.g., K/cmm, g/dL, %, mg/dL)
    /// </summary>
    [Id(11)]
    public string? ResultUnit { get; set; }

    /// <summary>
    /// Reference Range Low
    /// </summary>
    [Id(12)]
    public string? ReferenceRangeLow { get; set; }

    /// <summary>
    /// Reference Range High
    /// </summary>
    [Id(13)]
    public string? ReferenceRangeHigh { get; set; }

    /// <summary>
    /// Abnormal Flag (e.g., High, Low, Critical, Normal)
    /// </summary>
    [Id(14)]
    public string? AbnormalFlag { get; set; }

    /// <summary>
    /// Test Status (e.g., Ordered, Collected, Pending, Completed, Cancelled)
    /// </summary>
    [Id(15)]
    public string Status { get; set; } = "Ordered";

    /// <summary>
    /// Performing Lab/Location
    /// </summary>
    [Id(16)]
    public string? PerformingLab { get; set; }

    /// <summary>
    /// Ordering Provider IEN
    /// </summary>
    [Id(17)]
    public string? OrderingProviderId { get; set; }

    /// <summary>
    /// Ordering Provider Name
    /// </summary>
    [Id(18)]
    public string? OrderingProviderName { get; set; }

    /// <summary>
    /// Verifying Provider IEN
    /// </summary>
    [Id(19)]
    public string? VerifyingProviderId { get; set; }

    /// <summary>
    /// Verifying Provider Name
    /// </summary>
    [Id(20)]
    public string? VerifyingProviderName { get; set; }

    /// <summary>
    /// Verified Date/Time
    /// </summary>
    [Id(21)]
    public DateTime? VerifiedDateTime { get; set; }

    /// <summary>
    /// Test Comments/Remarks
    /// </summary>
    [Id(22)]
    public string? Comments { get; set; }

    /// <summary>
    /// Test Category (e.g., HEMATOLOGY, CHEMISTRY, MICROBIOLOGY)
    /// </summary>
    [Id(23)]
    public string? Category { get; set; }

    /// <summary>
    /// Critical Result Flag
    /// </summary>
    [Id(24)]
    public bool IsCritical { get; set; }

    /// <summary>
    /// Date Record Created
    /// </summary>
    [Id(25)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(26)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// lab test on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public LabTestState Clone() => new()
    {
        LabTestId = LabTestId,
        PatientId = PatientId,
        TestId = TestId,
        TestName = TestName,
        TestCode = TestCode,
        OrderId = OrderId,
        CollectionDateTime = CollectionDateTime,
        SpecimenType = SpecimenType,
        CollectionSample = CollectionSample,
        ResultDateTime = ResultDateTime,
        ResultValue = ResultValue,
        ResultUnit = ResultUnit,
        ReferenceRangeLow = ReferenceRangeLow,
        ReferenceRangeHigh = ReferenceRangeHigh,
        AbnormalFlag = AbnormalFlag,
        Status = Status,
        PerformingLab = PerformingLab,
        OrderingProviderId = OrderingProviderId,
        OrderingProviderName = OrderingProviderName,
        VerifyingProviderId = VerifyingProviderId,
        VerifyingProviderName = VerifyingProviderName,
        VerifiedDateTime = VerifiedDateTime,
        Comments = Comments,
        Category = Category,
        IsCritical = IsCritical,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate
    };
}
