// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lifecycle status of an EDI claim transmission batch.
/// </summary>
[GenerateSerializer]
public enum EdiTransmissionStatus
{
    /// <summary>Batch is being assembled; claims can still be added.</summary>
    Open = 0,

    /// <summary>Batch has been transmitted to the payer.</summary>
    Sent = 1,

    /// <summary>Payer has sent a functional acknowledgment (999/TA1).</summary>
    Acknowledged = 2,

    /// <summary>All claims in the batch were accepted by the payer.</summary>
    Accepted = 3,

    /// <summary>Some claims were accepted, some rejected.</summary>
    PartiallyAccepted = 4,

    /// <summary>All claims in the batch were rejected by the payer.</summary>
    Rejected = 5,
}

/// <summary>
/// Summary entry for a claim within a transmission batch.
/// </summary>
[GenerateSerializer]
public record EdiTransmissionClaimEntry
{
    /// <summary>EDI claim grain key string.</summary>
    [Id(0)] public string ClaimId { get; init; } = string.Empty;

    /// <summary>Patient associated with this claim.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Total billed amount for this claim.</summary>
    [Id(2)] public decimal TotalBilledAmount { get; init; }

    /// <summary>Claim type string (e.g., "Professional837P").</summary>
    [Id(3)] public string ClaimType { get; init; } = string.Empty;
}

/// <summary>
/// State for an EDI claim transmission batch sent to a single payer.
/// Maps to VistA File #361.1 (EDI TRANSMISSION LOG).
/// </summary>
[GenerateSerializer]
public class EdiTransmissionState
{
    /// <summary>Unique identifier for this transmission — grain key string.</summary>
    [Id(0)] public string TransmissionId { get; set; } = string.Empty;

    /// <summary>Human-readable batch number (e.g., "TX-2024-001").</summary>
    [Id(1)] public string BatchNumber { get; set; } = string.Empty;

    /// <summary>Payer (insurance company) identifier.</summary>
    [Id(2)] public string PayerId { get; set; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(3)] public string PayerName { get; set; } = string.Empty;

    /// <summary>Current status of the transmission batch.</summary>
    [Id(4)] public EdiTransmissionStatus Status { get; set; }

    /// <summary>Grain key strings of all claims in this batch.</summary>
    [Id(5)] public List<string> ClaimIds { get; set; } = new();

    /// <summary>Total number of claims in the batch.</summary>
    [Id(6)] public int TotalClaims { get; set; }

    /// <summary>Sum of billed amounts across all claims.</summary>
    [Id(7)] public decimal TotalBilledAmount { get; set; }

    /// <summary>Number of claims accepted by the payer (set on acknowledgment).</summary>
    [Id(8)] public int? AcceptedClaimsCount { get; set; }

    /// <summary>Number of claims rejected by the payer (set on acknowledgment).</summary>
    [Id(9)] public int? RejectedClaimsCount { get; set; }

    /// <summary>TA1 or 999 acknowledgment code from the payer.</summary>
    [Id(10)] public string? AcknowledgmentCode { get; set; }

    /// <summary>Date the payer returned the acknowledgment.</summary>
    [Id(11)] public DateTime? AcknowledgmentDate { get; set; }

    /// <summary>Date the batch was transmitted to the payer.</summary>
    [Id(12)] public DateTime? SentDate { get; set; }

    /// <summary>Optional free-text notes.</summary>
    [Id(13)] public string? Notes { get; set; }

    /// <summary>When this transmission record was created.</summary>
    [Id(14)] public DateTime CreatedDate { get; set; }

    /// <summary>When this transmission record was last modified.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; }
}
