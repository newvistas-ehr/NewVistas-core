// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Priority code for a VA Form 2237 purchase request.</summary>
[GenerateSerializer]
public enum PurchaseRequestPriority
{
    /// <summary>Standard delivery — no special urgency.</summary>
    Routine = 0,
    /// <summary>Needed sooner than routine; within a few days.</summary>
    Urgent = 1,
    /// <summary>Immediate need; patient care or safety at risk.</summary>
    Emergency = 2,
}

/// <summary>Lifecycle status of a VA Form 2237 purchase request.</summary>
[GenerateSerializer]
public enum PurchaseRequestStatus
{
    /// <summary>Request is being drafted by the requestor.</summary>
    Draft = 0,
    /// <summary>Request submitted to control point for approval.</summary>
    Submitted = 1,
    /// <summary>Request approved by the control point officer.</summary>
    Approved = 2,
    /// <summary>Purchase order has been issued against this request.</summary>
    Funded = 3,
    /// <summary>Request cancelled before a PO was issued.</summary>
    Cancelled = 4,
    /// <summary>Request rejected by the control point officer.</summary>
    Rejected = 5,
}

/// <summary>A single line item on a VA Form 2237 purchase request.</summary>
[GenerateSerializer]
public record PurchaseRequestLineItem(
    [property: Id(0)] int LineNumber,
    [property: Id(1)] string ItemDescription,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] string UnitOfIssue,
    [property: Id(4)] decimal UnitCost,
    [property: Id(5)] decimal ExtendedCost,
    [property: Id(6)] string? NSN,
    [property: Id(7)] string? FSSNumber,
    [property: Id(8)] string? Notes);

/// <summary>
/// State for a VA Form 2237 (Request for Procurement / Purchase Request).
/// Corresponds to VistA File #2237 (REQUEST FOR PROCUREMENT).
/// Grain key: "IFCAP-PR:{requestId}"
/// </summary>
[GenerateSerializer]
public class PurchaseRequestState
{
    /// <summary>Unique identifier for this request. (.01)</summary>
    [Id(0)] public string RequestId { get; set; } = string.Empty;

    /// <summary>Control point sponsoring this request. (.02)</summary>
    [Id(1)] public string ControlPointId { get; set; } = string.Empty;

    /// <summary>User ID of the originating requestor. (.03)</summary>
    [Id(2)] public string RequestorId { get; set; } = string.Empty;

    /// <summary>Name of the originating requestor. (.04)</summary>
    [Id(3)] public string RequestorName { get; set; } = string.Empty;

    /// <summary>Federal fiscal year this request is against. (.05)</summary>
    [Id(4)] public int FiscalYear { get; set; }

    /// <summary>Priority code (Routine / Urgent / Emergency). (.06)</summary>
    [Id(5)] public PurchaseRequestPriority Priority { get; set; } = PurchaseRequestPriority.Routine;

    /// <summary>Current lifecycle status. (.07)</summary>
    [Id(6)] public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;

    /// <summary>Date the request was created. (.08)</summary>
    [Id(7)] public DateTime RequestDate { get; set; }

    /// <summary>Date by which goods or services are needed. (.09)</summary>
    [Id(8)] public DateTime? NeedByDate { get; set; }

    /// <summary>Total estimated cost of all line items. (.10)</summary>
    [Id(9)] public decimal EstimatedCost { get; set; }

    /// <summary>Amount obligated against the control point upon approval. (.11)</summary>
    [Id(10)] public decimal ObligatedAmount { get; set; }

    /// <summary>Brief description of the items or services requested. (.12)</summary>
    [Id(11)] public string Description { get; set; } = string.Empty;

    /// <summary>Justification for the purchase. (.13)</summary>
    [Id(12)] public string Justification { get; set; } = string.Empty;

    /// <summary>Optional additional notes. (.14)</summary>
    [Id(13)] public string? Notes { get; set; }

    /// <summary>List of requested line items.</summary>
    [Id(14)] public List<PurchaseRequestLineItem> LineItems { get; set; } = new();

    /// <summary>Preferred vendor ID (optional). (.15)</summary>
    [Id(15)] public string? VendorId { get; set; }

    /// <summary>Preferred vendor name (optional). (.16)</summary>
    [Id(16)] public string? VendorName { get; set; }

    /// <summary>Purchase order ID assigned once funded. (.17)</summary>
    [Id(17)] public string? PurchaseOrderId { get; set; }

    /// <summary>User ID of the approver. (.18)</summary>
    [Id(18)] public string? ApprovedByUserId { get; set; }

    /// <summary>Name of the approver. (.19)</summary>
    [Id(19)] public string? ApprovedByUserName { get; set; }

    /// <summary>Date of approval. (.20)</summary>
    [Id(20)] public DateTime? ApprovalDate { get; set; }

    /// <summary>Reason for rejection (if rejected). (.21)</summary>
    [Id(21)] public string? RejectionReason { get; set; }

    /// <summary>Date the request was created.</summary>
    [Id(22)] public DateTime CreatedDate { get; set; }

    /// <summary>Date the request was last modified.</summary>
    [Id(23)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight summary of a purchase request for index queries.</summary>
[GenerateSerializer]
public record PurchaseRequestIndexEntry(
    [property: Id(0)] string RequestId,
    [property: Id(1)] string ControlPointId,
    [property: Id(2)] string RequestorName,
    [property: Id(3)] decimal EstimatedCost,
    [property: Id(4)] PurchaseRequestStatus Status,
    [property: Id(5)] PurchaseRequestPriority Priority,
    [property: Id(6)] DateTime RequestDate);

/// <summary>Per-control-point index state for purchase requests.</summary>
[GenerateSerializer]
public class PurchaseRequestIndexState
{
    [Id(0)] public List<PurchaseRequestIndexEntry> Entries { get; set; } = new();
}
