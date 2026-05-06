// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Labs;

/// <summary>
/// Causal event recording specimen collection for a previously-ordered lab
/// test — VistA LRFN COLLECT workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SpecimenCollectedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "LABS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string LabTestId { get; init; } = string.Empty;

    [Id(7)] public DateTime CollectionDateTime { get; init; }
    [Id(8)] public string? CollectionSample { get; init; }
    [Id(9)] public string? PerformingLab { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(SpecimenCollectedV1),
        LabTestId,
        CollectionDateTime.ToString("O"),
        CollectionSample ?? string.Empty,
        PerformingLab ?? string.Empty);
}
