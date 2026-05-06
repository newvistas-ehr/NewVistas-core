// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Labs;

/// <summary>
/// Causal event recording the placement of a new lab test order — VistA LAB
/// DATA file (#63), LRWU/LRFN order workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LabOrderedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "LABS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string LabTestId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the ordered lab test.</summary>
    [Id(7)] public LabTestState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(LabOrderedV1),
        LabTestId,
        Snapshot.PatientId,
        Snapshot.TestId,
        Snapshot.TestName,
        Snapshot.TestCode ?? string.Empty,
        Snapshot.OrderId ?? string.Empty,
        Snapshot.OrderingProviderId ?? string.Empty,
        Snapshot.SpecimenType ?? string.Empty,
        Snapshot.Category ?? string.Empty,
        Snapshot.Status);
}
