// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
