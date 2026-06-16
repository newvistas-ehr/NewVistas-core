// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for a single collection/dunning letter.
/// Maps to VistA PRCA collection correspondence (RCCLLT*.m, RCCL*.m).
/// Grain key: "AR-LETTER:{guid}"
/// </summary>
public interface ICollectionLetterGrain : IGrainWithStringKey
{
    /// <summary>Returns the current letter state.</summary>
    Task<CollectionLetterState> GetAsync();

    /// <summary>
    /// Generates a new collection letter for a patient. Sets Status = Generated.
    /// Computes line items from the patient's active AR accounts and generates letter body text.
    /// </summary>
    Task<string> GenerateAsync(
        string patientId,
        string patientName,
        string? mailingAddress,
        CollectionLetterType letterType,
        List<CollectionLetterLineItem> lineItems,
        decimal totalAmountDue,
        DateTime? paymentDueDate,
        int responseDays,
        int dunningSequence,
        string? generatedByUserId,
        string? generatedByUserName,
        string? notes);

    /// <summary>Marks the letter as printed. Sets Status = Printed.</summary>
    Task MarkPrintedAsync();

    /// <summary>Marks the letter as mailed. Sets Status = Mailed.</summary>
    Task MarkMailedAsync();

    /// <summary>Marks the letter as returned undeliverable. Sets Status = Returned.</summary>
    Task MarkReturnedAsync();

    /// <summary>Cancels the letter. Sets Status = Cancelled.</summary>
    Task CancelAsync(string? reason);
}
