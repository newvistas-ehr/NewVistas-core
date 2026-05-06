// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Consults;

/// <summary>
/// Causal event recording completion of a consult — transitions status to
/// COMPLETE and links the resulting note/document.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ConsultCompletedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "CONSULTS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string ConsultId { get; init; } = string.Empty;

    [Id(7)] public DateTime CompletedDateTime { get; init; }
    [Id(8)] public string? ResultDocumentId { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ConsultCompletedV1),
        ConsultId,
        CompletedDateTime.ToString("O"),
        ResultDocumentId ?? string.Empty);
}
