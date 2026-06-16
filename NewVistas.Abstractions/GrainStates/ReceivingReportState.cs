// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Status of an IFCAP receiving report.</summary>
[GenerateSerializer]
public enum ReceivingReportStatus
{
    /// <summary>Report is being drafted.</summary>
    Draft = 0,
    /// <summary>Goods accepted by receiving section.</summary>
    Accepted = 1,
    /// <summary>Goods rejected (returned to vendor or pending resolution).</summary>
    Rejected = 2,
}

/// <summary>A single line item on a receiving report.</summary>
[GenerateSerializer]
public record RrLineItem(
    [property: Id(0)] int LineNumber,
    [property: Id(1)] string ItemDescription,
    [property: Id(2)] decimal QuantityOrdered,
    [property: Id(3)] decimal QuantityReceived,
    [property: Id(4)] decimal QuantityAccepted,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] decimal ExtendedCost,
    [property: Id(7)] string? Condition);

/// <summary>
/// State for an IFCAP Receiving Report — documents physical receipt of goods.
/// Corresponds to VistA File #420.5 (RECEIVING REPORT).
/// Grain key: "IFCAP-RR:{rrId}"
/// </summary>
[GenerateSerializer]
public class ReceivingReportState
{
    /// <summary>Unique identifier for this receiving report. (.01)</summary>
    [Id(0)] public string ReceivingReportId { get; set; } = string.Empty;

    /// <summary>Purchase order this receiving report is for. (.02)</summary>
    [Id(1)] public string PurchaseOrderId { get; set; } = string.Empty;

    /// <summary>Control point associated with this receipt. (.03)</summary>
    [Id(2)] public string ControlPointId { get; set; } = string.Empty;

    /// <summary>Date goods were physically received. (.04)</summary>
    [Id(3)] public DateTime ReceivedDate { get; set; }

    /// <summary>User ID of the receiving clerk. (.05)</summary>
    [Id(4)] public string ReceivedByUserId { get; set; } = string.Empty;

    /// <summary>Name of the receiving clerk. (.06)</summary>
    [Id(5)] public string ReceivedByUserName { get; set; } = string.Empty;

    /// <summary>Current status of the receiving report. (.07)</summary>
    [Id(6)] public ReceivingReportStatus Status { get; set; } = ReceivingReportStatus.Draft;

    /// <summary>Line items received against this report.</summary>
    [Id(7)] public List<RrLineItem> LineItems { get; set; } = new();

    /// <summary>Optional receiving notes. (.08)</summary>
    [Id(8)] public string? Notes { get; set; }

    /// <summary>Reason for rejection (if rejected). (.09)</summary>
    [Id(9)] public string? RejectionReason { get; set; }

    /// <summary>Date the report was created.</summary>
    [Id(10)] public DateTime CreatedDate { get; set; }

    /// <summary>Date the report was last modified.</summary>
    [Id(11)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight summary of a receiving report for index queries.</summary>
[GenerateSerializer]
public record ReceivingReportIndexEntry(
    [property: Id(0)] string ReceivingReportId,
    [property: Id(1)] string PurchaseOrderId,
    [property: Id(2)] DateTime ReceivedDate,
    [property: Id(3)] ReceivingReportStatus Status,
    [property: Id(4)] string ReceivedByUserName,
    [property: Id(5)] decimal TotalAccepted);

/// <summary>Per-PO index state for receiving reports.</summary>
[GenerateSerializer]
public class ReceivingReportIndexState
{
    [Id(0)] public List<ReceivingReportIndexEntry> Entries { get; set; } = new();
}
