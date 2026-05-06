// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    Task<ARTransactionState> GetAsync();

    /// <summary>
    /// Creates the transaction record. Should be called exactly once per grain instance.
    /// </summary>
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
