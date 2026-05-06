// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Vitals;

/// <summary>
/// Causal event recording a vital measurement — VistA GMRV VITAL MEASUREMENT
/// file (#120.5). Records the as-measured snapshot including range-validation
/// flags computed at record time.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VitalRecordedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "VITALS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string VitalId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the vital measurement.</summary>
    [Id(7)] public VitalState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(VitalRecordedV1),
        VitalId,
        Snapshot.PatientId,
        Snapshot.VitalType,
        Snapshot.Value,
        Snapshot.Units ?? string.Empty,
        Snapshot.DateTimeTaken.ToString("O"),
        Snapshot.LocationId ?? string.Empty,
        Snapshot.EnteredById ?? string.Empty,
        string.Join(",", Snapshot.Qualifiers),
        Snapshot.AbnormalFlag ?? string.Empty,
        Snapshot.Comments ?? string.Empty);
}
