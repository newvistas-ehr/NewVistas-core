// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Adt;

/// <summary>
/// Causal event recording an inter-ward/inter-specialty transfer — VistA
/// PATIENT MOVEMENT file (#405), TransactionType = "TRANSFER". A transfer
/// creates a new movement grain rather than mutating the admission grain.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TransferRecordedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "ADT";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string MovementId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the transfer movement (post-transfer state).</summary>
    [Id(7)] public AdtState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(TransferRecordedV1),
        MovementId,
        Snapshot.PatientId,
        Snapshot.AdmissionDateTime?.ToString("O") ?? string.Empty,
        Snapshot.MovementDateTime.ToString("O"),
        Snapshot.WardLocationId ?? string.Empty,
        Snapshot.RoomBed ?? string.Empty,
        Snapshot.TreatingSpecialtyId ?? string.Empty,
        Snapshot.AttendingPhysicianId ?? string.Empty);
}
