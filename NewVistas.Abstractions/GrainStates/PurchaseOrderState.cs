// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Lifecycle status of an IFCAP purchase order.</summary>
[GenerateSerializer]
public enum PurchaseOrderStatus
{
    /// <summary>PO is being drafted before issuance.</summary>
    Draft = 0,
    /// <summary>PO has been issued to the vendor; awaiting delivery.</summary>
    Open = 1,
    /// <summary>Some line items received; awaiting remaining delivery.</summary>
    PartiallyReceived = 2,
    /// <summary>All line items fully received.</summary>
    Received = 3,
    /// <summary>PO has been cancelled.</summary>
    Cancelled = 4,
    /// <summary>PO finalized / closed out.</summary>
    Closed = 5,
}

/// <summary>A single line item on a purchase order.</summary>
[GenerateSerializer]
public record PoLineItem(
    [property: Id(0)] int LineNumber,
    [property: Id(1)] string ItemDescription,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] decimal QuantityReceived,
    [property: Id(4)] string UnitOfIssue,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] decimal ExtendedCost,
    [property: Id(7)] string? NSN,
    [property: Id(8)] string? FSSNumber);

/// <summary>
/// State for an IFCAP Purchase Order issued to a vendor.
/// Corresponds to VistA File #442 (PURCHASE ORDER).
/// Grain key: "IFCAP-PO:{poId}"
/// </summary>
[GenerateSerializer]
public class PurchaseOrderState
{
    /// <summary>Unique purchase order identifier / PO number. (.01)</summary>
    [Id(0)] public string PurchaseOrderId { get; set; } = string.Empty;

    /// <summary>Purchase request that originated this PO. (.02)</summary>
    [Id(1)] public string RequestId { get; set; } = string.Empty;

    /// <summary>Control point sponsoring this PO. (.03)</summary>
    [Id(2)] public string ControlPointId { get; set; } = string.Empty;

    /// <summary>Vendor ID. (.04)</summary>
    [Id(3)] public string VendorId { get; set; } = string.Empty;

    /// <summary>Vendor name. (.05)</summary>
    [Id(4)] public string VendorName { get; set; } = string.Empty;

    /// <summary>Vendor delivery address. (.06)</summary>
    [Id(5)] public string? VendorAddress { get; set; }

    /// <summary>Current lifecycle status. (.07)</summary>
    [Id(6)] public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    /// <summary>Date the PO was issued. (.08)</summary>
    [Id(7)] public DateTime IssuedDate { get; set; }

    /// <summary>Required delivery date. (.09)</summary>
    [Id(8)] public DateTime RequiredDeliveryDate { get; set; }

    /// <summary>Actual delivery completion date. (.10)</summary>
    [Id(9)] public DateTime? ActualDeliveryDate { get; set; }

    /// <summary>Total dollar value of the PO. (.11)</summary>
    [Id(10)] public decimal TotalAmount { get; set; }

    /// <summary>Total amount received to date. (.12)</summary>
    [Id(11)] public decimal AmountReceived { get; set; }

    /// <summary>Line items on this purchase order.</summary>
    [Id(12)] public List<PoLineItem> LineItems { get; set; } = new();

    /// <summary>User ID of the issuing officer. (.13)</summary>
    [Id(13)] public string IssuedByUserId { get; set; } = string.Empty;

    /// <summary>Name of the issuing officer. (.14)</summary>
    [Id(14)] public string IssuedByUserName { get; set; } = string.Empty;

    /// <summary>IDs of receiving reports filed against this PO.</summary>
    [Id(15)] public List<string> ReceivingReportIds { get; set; } = new();

    /// <summary>Reason for cancellation (if cancelled). (.16)</summary>
    [Id(16)] public string? CancellationReason { get; set; }

    /// <summary>Date the PO was created.</summary>
    [Id(17)] public DateTime CreatedDate { get; set; }

    /// <summary>Date the PO was last modified.</summary>
    [Id(18)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight summary of a purchase order for index queries.</summary>
[GenerateSerializer]
public record PurchaseOrderIndexEntry(
    [property: Id(0)] string PurchaseOrderId,
    [property: Id(1)] string ControlPointId,
    [property: Id(2)] string VendorName,
    [property: Id(3)] decimal TotalAmount,
    [property: Id(4)] decimal AmountReceived,
    [property: Id(5)] PurchaseOrderStatus Status,
    [property: Id(6)] DateTime IssuedDate,
    [property: Id(7)] string RequestId);

/// <summary>Global singleton index state for all purchase orders.</summary>
[GenerateSerializer]
public class PurchaseOrderIndexState
{
    [Id(0)] public List<PurchaseOrderIndexEntry> Entries { get; set; } = new();
}
