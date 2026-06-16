// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab EDI Order Grain Interface — represents an electronic lab order
/// transmitted to an external reference laboratory.
///
/// Grain Key: "LAB-EDI-ORD:{guid}"
///
/// Part of the Lab EDI (Electronic Data Interchange) subsystem for external
/// reference lab ordering. Sends ORM^O01 orders out and tracks lifecycle
/// through transmission, acknowledgment, result receipt, or rejection.
///
/// Extends the existing Lab Instrument infrastructure (LA package) with
/// outbound order management for send-out testing.
/// </summary>
public interface ILabEdiOrderGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete EDI order state.
    /// </summary>
    Task<LabEdiOrderState> GetOrderAsync();

    /// <summary>
    /// Creates a new electronic lab order for transmission to a reference lab.
    /// </summary>
    Task CreateOrderAsync(
        string patientId,
        string patientName,
        string referenceLab,
        string referenceLabId,
        string orderingProviderId,
        string orderingProviderName,
        List<LabEdiTestRequest> testsRequested,
        string? clinicalHistory,
        string? specimenType,
        string? specimenId,
        DateTime? collectionDate,
        string? priority);

    /// <summary>
    /// Marks the order as transmitted to the reference lab via HL7 ORM message.
    /// </summary>
    Task MarkTransmittedAsync(string hl7MessageId, DateTime transmittedDate);

    /// <summary>
    /// Marks the order as acknowledged by the reference lab (ACK received).
    /// </summary>
    Task MarkAcknowledgedAsync(string acknowledgmentCode, DateTime acknowledgedDate);

    /// <summary>
    /// Marks the order as having results received from the reference lab.
    /// </summary>
    Task MarkResultReceivedAsync(DateTime receivedDate);

    /// <summary>
    /// Marks the order as rejected by the reference lab.
    /// </summary>
    Task MarkRejectedAsync(string rejectionReason, DateTime rejectedDate);

    /// <summary>
    /// Cancels the order before or after transmission.
    /// </summary>
    Task CancelOrderAsync(string reason, string cancelledBy);
}
