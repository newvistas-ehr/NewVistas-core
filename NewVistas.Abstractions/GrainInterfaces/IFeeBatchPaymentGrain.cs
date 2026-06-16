// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for a fee basis batch payment run.
/// Groups approved invoices for bulk vendor disbursement via check or EFT.
/// Maps to VistA File #162.7 (FEE BASIS BATCH PAYMENT).
/// MUMPS: FBCH*.m, FBAA*.m
/// Grain key: "FEE-BATCH:{guid}"
/// </summary>
public interface IFeeBatchPaymentGrain : IGrainWithStringKey
{
    /// <summary>Returns the current batch state.</summary>
    Task<FeeBatchPaymentState> GetAsync();

    /// <summary>
    /// Initializes a new batch payment run.
    /// </summary>
    Task CreateAsync(
        string? vendorId,
        string? vendorName,
        DateTime batchDate,
        string paymentMethod,
        string? checkNumber,
        string? notes);

    /// <summary>
    /// Adds an approved invoice to this batch. Increments TotalAmount.
    /// </summary>
    Task AddInvoiceAsync(
        string invoiceId,
        string authorizationId,
        string patientId,
        string patientName,
        string vendorId,
        string vendorName,
        decimal paidAmount);

    /// <summary>
    /// Posts the batch: calls IFeeInvoiceGrain.PayAsync for each invoice,
    /// which in turn calls IFeeAuthorizationGrain.RecordInvoicePaymentAsync.
    /// Sets IsPosted = true, PostedDate = UtcNow.
    /// </summary>
    Task PostAsync(string postedByUserId, string postedByUserName);
}
