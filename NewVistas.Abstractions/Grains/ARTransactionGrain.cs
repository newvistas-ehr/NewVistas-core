// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARTransactionGrain : Grain, IARTransactionGrain
{
    private readonly IPersistentState<ARTransactionState> _state;

    public ARTransactionGrain(
        [PersistentState("arTransactionState", "arTransactionStore")]
        IPersistentState<ARTransactionState> state)
    {
        _state = state;
    }

    public Task<ARTransactionState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string arAccountId,
        string patientId,
        ARTransactionType transactionType,
        decimal amount,
        string appliedByUserId,
        string appliedByUserName,
        string? receiptNumber,
        string? checkNumber,
        string? paymentMethod,
        string? referenceNumber,
        string? notes)
    {
        _state.State.TransactionId     = this.GetPrimaryKeyString();
        _state.State.ARAccountId       = arAccountId;
        _state.State.PatientId         = patientId;
        _state.State.TransactionType   = transactionType;
        _state.State.Amount            = amount;
        _state.State.TransactionDate   = DateTime.UtcNow;
        _state.State.AppliedByUserId   = appliedByUserId;
        _state.State.AppliedByUserName = appliedByUserName;
        _state.State.ReceiptNumber     = receiptNumber;
        _state.State.CheckNumber       = checkNumber;
        _state.State.PaymentMethod     = paymentMethod;
        _state.State.ReferenceNumber   = referenceNumber;
        _state.State.Notes             = notes;
        _state.State.CreatedDate       = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
