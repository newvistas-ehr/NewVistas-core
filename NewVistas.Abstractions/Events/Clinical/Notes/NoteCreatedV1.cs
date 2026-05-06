// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Notes;

/// <summary>
/// Causal event recording the creation of a new TIU document — VistA TIU
/// DOCUMENT file (#8925), TIUSRVP1 SAVE workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NoteCreatedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "NOTES";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string DocumentId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the note as created (UNSIGNED).</summary>
    [Id(7)] public TiuDocumentState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(NoteCreatedV1),
        DocumentId,
        Snapshot.PatientId,
        Snapshot.DocumentType,
        Snapshot.DocumentTypeId ?? string.Empty,
        Snapshot.Subject ?? string.Empty,
        Snapshot.AuthorId ?? string.Empty,
        Snapshot.CosignerId ?? string.Empty,
        Snapshot.LocationId ?? string.Empty,
        Snapshot.VisitId ?? string.Empty,
        Snapshot.ReferenceDate.ToString("O"),
        Snapshot.ReportText);
}
