// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CashierReceiptGrain : Grain, ICashierReceiptGrain
{
    private readonly IPersistentState<CashierReceiptState> _state;

    public CashierReceiptGrain(
        [PersistentState("cashierReceiptState", "cashierReceiptStore")]
        IPersistentState<CashierReceiptState> state)
    {
        _state = state;
    }

    public Task<CashierReceiptState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task IssueAsync(
        string receiptNumber,
        string patientId,
        string patientName,
        string arAccountId,
        decimal amount,
        CashierPaymentMethod paymentMethod,
        string cashierId,
        string cashierName,
        string sessionId,
        string? checkNumber,
        string? notes)
    {
        string receiptId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;

        _state.State.ReceiptId        = receiptId;
        _state.State.ReceiptNumber    = receiptNumber;
        _state.State.PatientId        = patientId;
        _state.State.PatientName      = patientName;
        _state.State.ARAccountId      = arAccountId;
        _state.State.Amount           = amount;
        _state.State.PaymentMethod    = paymentMethod;
        _state.State.Status           = CashierReceiptStatus.Issued;
        _state.State.CashierId        = cashierId;
        _state.State.CashierName      = cashierName;
        _state.State.SessionId        = sessionId;
        _state.State.ReceiptDate      = now;
        _state.State.CheckNumber      = checkNumber;
        _state.State.Notes            = notes;
        _state.State.CreatedDate      = now;
        _state.State.LastModifiedDate = now;

        // Post payment to the linked AR account
        IARAccountGrain arAccount = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}");
        await arAccount.PostPaymentAsync(
            amount,
            paymentMethod.ToString(),
            cashierId,
            cashierName,
            receiptId,
            checkNumber,
            $"Receipt {receiptNumber}");

        // Record receipt in the cashier session running totals
        ICashierSessionGrain session = GrainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{sessionId}");
        await session.RecordReceiptAsync(receiptId, amount, paymentMethod.ToString(), now);

        await _state.WriteStateAsync();
    }

    public async Task VoidAsync(string reason, string voidedByUserId)
    {
        _state.State.Status           = CashierReceiptStatus.Voided;
        _state.State.VoidReason       = reason;
        _state.State.VoidedByUserId   = voidedByUserId;
        _state.State.VoidedDate       = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
