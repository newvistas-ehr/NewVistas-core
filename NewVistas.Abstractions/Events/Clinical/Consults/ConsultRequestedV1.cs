// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Consults;

/// <summary>
/// Causal event recording the request of a new consult — VistA
/// REQUEST/CONSULTATION file (#123) GMRC consult request workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ConsultRequestedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "CONSULTS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string ConsultId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the consult as requested.</summary>
    [Id(7)] public ConsultState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(ConsultRequestedV1),
        ConsultId,
        Snapshot.PatientId,
        Snapshot.ToService,
        Snapshot.ToServiceId ?? string.Empty,
        Snapshot.FromService ?? string.Empty,
        Snapshot.Urgency,
        Snapshot.RequestingProviderId ?? string.Empty,
        Snapshot.AttentionProviderId ?? string.Empty,
        Snapshot.ReasonForRequest ?? string.Empty,
        Snapshot.ProvisionalDiagnosis ?? string.Empty,
        Snapshot.OrderId ?? string.Empty,
        Snapshot.LocationId ?? string.Empty,
        Snapshot.RequestDateTime.ToString("O"));
}
