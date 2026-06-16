// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PurchaseRequestGrain : Grain, IPurchaseRequestGrain
{
    private readonly IPersistentState<PurchaseRequestState> _state;

    public PurchaseRequestGrain(
        [PersistentState("purchaseRequestState", "ifcapPurchaseRequestStore")]
        IPersistentState<PurchaseRequestState> state)
    {
        _state = state;
    }

    public Task<PurchaseRequestState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
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
        string? notes)
    {
        _state.State.RequestId       = this.GetPrimaryKeyString();
        _state.State.ControlPointId  = controlPointId;
        _state.State.RequestorId     = requestorId;
        _state.State.RequestorName   = requestorName;
        _state.State.FiscalYear      = fiscalYear;
        _state.State.Priority        = priority;
        _state.State.Status          = PurchaseRequestStatus.Draft;
        _state.State.RequestDate     = DateTime.UtcNow;
        _state.State.NeedByDate      = needByDate;
        _state.State.EstimatedCost   = estimatedCost;
        _state.State.Description     = description;
        _state.State.Justification   = justification;
        _state.State.Notes           = notes;
        _state.State.LineItems       = lineItems;
        _state.State.VendorId        = vendorId;
        _state.State.VendorName      = vendorName;
        _state.State.CreatedDate     = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SubmitAsync(string submittedByUserId)
    {
        _state.State.Status           = PurchaseRequestStatus.Submitted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(string approvedByUserId, string approvedByUserName)
    {
        _state.State.Status              = PurchaseRequestStatus.Approved;
        _state.State.ApprovedByUserId    = approvedByUserId;
        _state.State.ApprovedByUserName  = approvedByUserName;
        _state.State.ApprovalDate        = DateTime.UtcNow;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectAsync(string rejectedByUserId, string reason)
    {
        _state.State.Status           = PurchaseRequestStatus.Rejected;
        _state.State.RejectionReason  = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FundAsync(string purchaseOrderId)
    {
        _state.State.Status           = PurchaseRequestStatus.Funded;
        _state.State.PurchaseOrderId  = purchaseOrderId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason)
    {
        _state.State.Status           = PurchaseRequestStatus.Cancelled;
        _state.State.RejectionReason  = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
