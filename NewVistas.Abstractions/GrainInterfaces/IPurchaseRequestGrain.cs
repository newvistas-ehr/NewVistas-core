// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the lifecycle of a single VA Form 2237 (Request for Procurement).
/// Grain key: "IFCAP-PR:{requestId}"
/// </summary>
public interface IPurchaseRequestGrain : IGrainWithStringKey
{
    /// <summary>Returns the current purchase request state.</summary>
    Task<PurchaseRequestState> GetAsync();

    /// <summary>
    /// Creates the purchase request in Draft status.
    /// </summary>
    Task CreateAsync(
        string controlPointId,
        string requestorId,
        string requestorName,
        int fiscalYear,
        PurchaseRequestPriority priority,
        DateTime? needByDate,
        string description,
        string justification,
        decimal estimatedCost,
        List<PurchaseRequestLineItem> lineItems,
        string? vendorId,
        string? vendorName,
        string? notes);

    /// <summary>
    /// Submits the request to the control point for approval.
    /// Sets Status = Submitted.
    /// </summary>
    Task SubmitAsync(string submittedByUserId);

    /// <summary>
    /// Approves the request. Sets Status = Approved, records approver and date.
    /// </summary>
    Task ApproveAsync(string approvedByUserId, string approvedByUserName);

    /// <summary>
    /// Rejects the request. Sets Status = Rejected, records rejection reason.
    /// </summary>
    Task RejectAsync(string rejectedByUserId, string reason);

    /// <summary>
    /// Marks the request as funded once a purchase order is issued.
    /// Sets Status = Funded and records the PO ID.
    /// </summary>
    Task FundAsync(string purchaseOrderId);

    /// <summary>
    /// Cancels the request before a PO is issued.
    /// Sets Status = Cancelled.
    /// </summary>
    Task CancelAsync(string reason);
}
