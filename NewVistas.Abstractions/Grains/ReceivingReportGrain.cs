// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ReceivingReportGrain : Grain, IReceivingReportGrain
{
    private readonly IPersistentState<ReceivingReportState> _state;

    public ReceivingReportGrain(
        [PersistentState("receivingReportState", "ifcapReceivingReportStore")]
        IPersistentState<ReceivingReportState> state)
    {
        _state = state;
    }

    public Task<ReceivingReportState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string purchaseOrderId,
        string controlPointId,
        string receivedByUserId,
        string receivedByUserName,
        List<RrLineItem> lineItems,
        string? notes)
    {
        _state.State.ReceivingReportId   = this.GetPrimaryKeyString();
        _state.State.PurchaseOrderId     = purchaseOrderId;
        _state.State.ControlPointId      = controlPointId;
        _state.State.ReceivedDate        = DateTime.UtcNow;
        _state.State.ReceivedByUserId    = receivedByUserId;
        _state.State.ReceivedByUserName  = receivedByUserName;
        _state.State.Status              = ReceivingReportStatus.Draft;
        _state.State.LineItems           = lineItems;
        _state.State.Notes               = notes;
        _state.State.CreatedDate         = DateTime.UtcNow;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcceptAsync(string acceptedByUserId)
    {
        _state.State.Status           = ReceivingReportStatus.Accepted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectAsync(string reason, string rejectedByUserId)
    {
        _state.State.Status           = ReceivingReportStatus.Rejected;
        _state.State.RejectionReason  = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
