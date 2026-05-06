// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Individual cashier receipt grain, created when a patient tenders payment at the
/// VA cashier window (VistA File #36 AGENT CASHIER receipt subfile).
/// Grain key: "CASHIER-RECEIPT:{guid}".
/// Managed by RCDPE.m, RCDPR.m MUMPS routines.
/// </summary>
public interface ICashierReceiptGrain : IGrainWithStringKey
{
    /// <summary>Returns the current receipt state.</summary>
    Task<CashierReceiptState> GetAsync();

    /// <summary>
    /// Issues a new receipt for a patient payment. Should only be called once per grain.
    /// Automatically posts the payment to the linked AR account via IARAccountGrain,
    /// and records the receipt in the cashier session via ICashierSessionGrain.
    /// </summary>
    Task IssueAsync(
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
        string? notes);

    /// <summary>
    /// Voids this receipt. Does not automatically reverse the AR payment —
    /// a manual AR adjustment is required if the payment needs to be reversed.
    /// </summary>
    Task VoidAsync(string reason, string voidedByUserId);
}
