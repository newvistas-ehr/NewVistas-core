// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Transactions.Abstractions;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Immutable record of a single AR financial transaction (VistA File #433).
/// Uses Orleans <see cref="ITransactionalState{T}"/> so that creating the
/// transaction record and updating the owning <see cref="ARAccountGrain"/> balance
/// commit (or roll back) as one ACID unit — money cannot be recorded against an
/// account whose balance was not updated, or vice versa.
/// </summary>
public class ARTransactionGrain : Grain, IARTransactionGrain
{
    private readonly ITransactionalState<ARTransactionState> _state;

    public ARTransactionGrain(
        [TransactionalState("arTransactionState", "arTransactionStore")]
        ITransactionalState<ARTransactionState> state)
    {
        _state = state;
    }

    public Task<ARTransactionState> GetAsync()
        => _state.PerformRead(s => s);

    public Task CreateAsync(
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
        string transactionId = this.GetPrimaryKeyString();
        return _state.PerformUpdate(s =>
        {
            s.TransactionId     = transactionId;
            s.ARAccountId       = arAccountId;
            s.PatientId         = patientId;
            s.TransactionType   = transactionType;
            s.Amount            = amount;
            s.TransactionDate   = DateTime.UtcNow;
            s.AppliedByUserId   = appliedByUserId;
            s.AppliedByUserName = appliedByUserName;
            s.ReceiptNumber     = receiptNumber;
            s.CheckNumber       = checkNumber;
            s.PaymentMethod     = paymentMethod;
            s.ReferenceNumber   = referenceNumber;
            s.Notes             = notes;
            s.CreatedDate       = DateTime.UtcNow;
        });
    }
}
