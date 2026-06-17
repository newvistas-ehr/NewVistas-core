// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Immutable record of a single financial transaction posted against an AR account
/// (VistA File #433 AR TRANSACTION).
/// Grain key: "AR-TXN:{txnId}"
/// Managed by RCDP*.m MUMPS routines.
/// </summary>
public interface IARTransactionGrain : IGrainWithStringKey
{
    /// <summary>Returns the transaction record.</summary>
    [Transaction(TransactionOption.CreateOrJoin)]
    Task<ARTransactionState> GetAsync();

    /// <summary>
    /// Creates the transaction record. Should be called exactly once per grain instance.
    /// Joins the ambient AR account transaction so the record and the account balance
    /// update commit atomically.
    /// </summary>
    [Transaction(TransactionOption.CreateOrJoin)]
    Task CreateAsync(
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
        string? notes);
}
