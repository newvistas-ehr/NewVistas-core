// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for cashier receipts.
/// Grain key: "CASHIER-RECEIPT-IDX:{patientId}".
/// </summary>
public interface ICashierReceiptIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new receipt entry or updates an existing one (matched by ReceiptId).</summary>
    Task AddOrUpdateAsync(CashierReceiptIndexEntry entry);

    /// <summary>Returns all receipt entries for this patient.</summary>
    Task<List<CashierReceiptIndexEntry>> GetAllAsync();
}
