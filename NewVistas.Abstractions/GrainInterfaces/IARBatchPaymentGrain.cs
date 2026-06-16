// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a batch payment session grouping multiple AR account payments
/// (VistA File #344 AR BATCH PAYMENT).
/// Grain key: "AR-BATCH:{batchId}"
/// Managed by RCDP*.m MUMPS routines.
/// </summary>
public interface IARBatchPaymentGrain : IGrainWithStringKey
{
    /// <summary>Returns the current batch payment state.</summary>
    Task<ARBatchPaymentState> GetAsync();

    /// <summary>
    /// Initializes a new batch payment session.
    /// Should be called exactly once per grain instance.
    /// </summary>
    Task CreateAsync(
        string? facilityId,
        string? facilityName,
        DateTime batchDate,
        string paymentMethod,
        string? checkNumber,
        string? notes);

    /// <summary>
    /// Adds a single payment line to the batch.
    /// Increments TotalAmount.
    /// </summary>
    Task AddPaymentAsync(
        string arAccountId,
        string patientId,
        string patientName,
        decimal amount,
        string? receiptNumber);

    /// <summary>
    /// Posts all payment lines in the batch to the corresponding ARAccountGrains,
    /// marks the batch as posted, and updates the singleton batch index.
    /// </summary>
    Task PostAsync(string postedByUserId, string postedByUserName);
}
