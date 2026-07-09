// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Transfers;

/// <summary>
/// Causal event recording a new inter-facility transfer request — the transfer-center
/// analog of ConsultRequestedV1; produces File #405 movements on completion.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TransferRequestedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "TRANSFERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string TransferId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the request as submitted.</summary>
    [Id(7)] public TransferRequestState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(TransferRequestedV1),
        TransferId,
        Snapshot.PatientId,
        Snapshot.SendingInstitutionId,
        Snapshot.ReceivingInstitutionId,
        Snapshot.RequestedLevelOfCare ?? string.Empty,
        Snapshot.Urgency,
        Snapshot.ReasonForTransfer ?? string.Empty,
        Snapshot.RequestDateTime?.ToString("O") ?? string.Empty);
}

/// <summary>The receiving facility accepted the transfer and reserved a bed.</summary>
[GenerateSerializer, Immutable]
public sealed record TransferAcceptedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "TRANSFERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string TransferId { get; init; } = string.Empty;
    [Id(7)] public string ReceivingInstitutionId { get; init; } = string.Empty;
    [Id(8)] public string? ReservedUnitId { get; init; }
    [Id(9)] public string? ReservedBedId { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(TransferAcceptedV1), TransferId, ReceivingInstitutionId,
        ReservedUnitId ?? string.Empty, ReservedBedId ?? string.Empty, OccurredUtc.ToString("O"));
}

/// <summary>The patient arrived: discharge at the sender + admission at the receiver.</summary>
[GenerateSerializer, Immutable]
public sealed record TransferCompletedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "TRANSFERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string TransferId { get; init; } = string.Empty;
    [Id(7)] public string? DischargeAdtId { get; init; }
    [Id(8)] public string? AdmissionAdtId { get; init; }
    [Id(9)] public DateTime ArrivalDateTime { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(TransferCompletedV1), TransferId,
        DischargeAdtId ?? string.Empty, AdmissionAdtId ?? string.Empty, ArrivalDateTime.ToString("O"));
}

/// <summary>The receiving facility declined the transfer (terminal, from REQUESTED only).</summary>
[GenerateSerializer, Immutable]
public sealed record TransferDeclinedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "TRANSFERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string TransferId { get; init; } = string.Empty;
    [Id(7)] public string? Reason { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(TransferDeclinedV1), TransferId, Reason ?? string.Empty, OccurredUtc.ToString("O"));
}

/// <summary>The sender cancelled the transfer (terminal; releases any bed reservation).</summary>
[GenerateSerializer, Immutable]
public sealed record TransferCancelledV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "TRANSFERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string TransferId { get; init; } = string.Empty;
    [Id(7)] public string? Reason { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(TransferCancelledV1), TransferId, Reason ?? string.Empty, OccurredUtc.ToString("O"));
}
