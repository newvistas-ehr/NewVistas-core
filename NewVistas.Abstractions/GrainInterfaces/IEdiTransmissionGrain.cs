// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for an EDI claim transmission batch.
/// Maps to VistA File #361.1 (EDI TRANSMISSION LOG).
/// Grain key: "EDI-TX:{guid}"
/// </summary>
public interface IEdiTransmissionGrain : IGrainWithStringKey
{
    /// <summary>Returns the current transmission state.</summary>
    Task<EdiTransmissionState> GetAsync();

    /// <summary>
    /// Opens a new transmission batch. Sets Status = Open.
    /// </summary>
    Task OpenAsync(string batchNumber, string payerId, string payerName, string? notes);

    /// <summary>
    /// Adds a claim to this batch. Increments TotalClaims and TotalBilledAmount.
    /// </summary>
    Task AddClaimAsync(string claimId, string patientId, decimal totalBilledAmount, string claimType);

    /// <summary>
    /// Marks the batch as sent to the payer. Sets Status = Sent and SentDate.
    /// </summary>
    Task SendAsync(DateTime sentDate);

    /// <summary>
    /// Records the payer's functional acknowledgment (999/TA1).
    /// Status = Accepted if rejectedCount == 0; Rejected if acceptedCount == 0; else PartiallyAccepted.
    /// </summary>
    Task RecordAcknowledgmentAsync(
        string acknowledgmentCode,
        int acceptedCount,
        int rejectedCount,
        DateTime acknowledgedDate);
}
