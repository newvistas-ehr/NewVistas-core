// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Notes;

/// <summary>
/// Causal event recording the electronic signature of a TIU document.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NoteSignedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "NOTES";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string DocumentId { get; init; } = string.Empty;

    /// <summary>UTC instant the signature was recorded.</summary>
    [Id(7)] public DateTime SignedDateTime { get; init; }

    /// <summary>Status the document transitions to (UNCOSIGNED if cosigner required, else COMPLETED).</summary>
    [Id(8)] public string ResultingStatus { get; init; } = string.Empty;

    public string Canonicalize() => string.Join("|",
        nameof(NoteSignedV1),
        DocumentId,
        SignedDateTime.ToString("O"),
        ResultingStatus);
}
