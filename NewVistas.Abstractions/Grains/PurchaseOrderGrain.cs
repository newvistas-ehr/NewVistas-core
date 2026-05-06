// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PurchaseOrderGrain : Grain, IPurchaseOrderGrain
{
    private readonly IPersistentState<PurchaseOrderState> _state;

    public PurchaseOrderGrain(
        [PersistentState("purchaseOrderState", "ifcapPurchaseOrderStore")]
        IPersistentState<PurchaseOrderState> state)
    {
        _state = state;
    }

    public Task<PurchaseOrderState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string requestId,
        string controlPointId,
        string vendorId,
        string vendorName,
        string? vendorAddress,
        DateTime requiredDeliveryDate,
        List<PoLineItem> lineItems,
        string issuedByUserId,
        string issuedByUserName)
    {
        _state.State.PurchaseOrderId      = this.GetPrimaryKeyString();
        _state.State.RequestId            = requestId;
        _state.State.ControlPointId       = controlPointId;
        _state.State.VendorId             = vendorId;
        _state.State.VendorName           = vendorName;
        _state.State.VendorAddress        = vendorAddress;
        _state.State.Status               = PurchaseOrderStatus.Open;
        _state.State.IssuedDate           = DateTime.UtcNow;
        _state.State.RequiredDeliveryDate = requiredDeliveryDate;
        _state.State.LineItems            = lineItems;
        _state.State.TotalAmount          = lineItems.Sum(l => l.ExtendedCost);
        _state.State.AmountReceived       = 0m;
        _state.State.IssuedByUserId       = issuedByUserId;
        _state.State.IssuedByUserName     = issuedByUserName;
        _state.State.CreatedDate          = DateTime.UtcNow;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordReceiptAsync(
        string receivingReportId,
        List<(int LineNumber, decimal QuantityAccepted)> lineReceipts)
    {
        // Update QuantityReceived on matching line items
        List<PoLineItem> updated = new();
        foreach (PoLineItem line in _state.State.LineItems)
        {
            (int ln, decimal qa) = lineReceipts.FirstOrDefault(r => r.LineNumber == line.LineNumber);
            if (ln == line.LineNumber && qa > 0)
                updated.Add(line with { QuantityReceived = line.QuantityReceived + qa });
            else
                updated.Add(line);
        }
        _state.State.LineItems = updated;

        // Recalculate amount received
        _state.State.AmountReceived = _state.State.LineItems.Sum(l => l.QuantityReceived * l.UnitCost);

        // Determine new status
        bool allReceived = _state.State.LineItems.All(l => l.QuantityReceived >= l.Quantity);
        _state.State.Status = allReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;

        if (allReceived)
            _state.State.ActualDeliveryDate = DateTime.UtcNow;

        if (!_state.State.ReceivingReportIds.Contains(receivingReportId))
            _state.State.ReceivingReportIds.Add(receivingReportId);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason, string cancelledByUserId)
    {
        _state.State.Status               = PurchaseOrderStatus.Cancelled;
        _state.State.CancellationReason   = reason;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseAsync(string closedByUserId)
    {
        _state.State.Status           = PurchaseOrderStatus.Closed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
