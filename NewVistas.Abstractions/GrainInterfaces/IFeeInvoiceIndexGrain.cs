// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for fee basis invoices.
/// Grain key: "FEE-INVOICE-IDX:{patientId}".
/// </summary>
public interface IFeeInvoiceIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new invoice entry or updates an existing one (matched by InvoiceId).</summary>
    Task AddOrUpdateAsync(FeeInvoiceIndexEntry entry);

    /// <summary>Returns all invoice entries for this patient.</summary>
    Task<List<FeeInvoiceIndexEntry>> GetAllAsync();

    /// <summary>Returns invoice entries filtered by status string (e.g., "Received", "Approved").</summary>
    Task<List<FeeInvoiceIndexEntry>> GetByStatusAsync(string status);
}
