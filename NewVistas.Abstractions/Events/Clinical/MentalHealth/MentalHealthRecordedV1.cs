// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.MentalHealth;

/// <summary>
/// Causal event recording the administration of a mental-health screening
/// instrument (PHQ-9, GAD-7, AUDIT-C, PC-PTSD-5, Columbia, etc.) — VistA YS
/// MH INSTRUMENT (#601.71).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MentalHealthRecordedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "MENTAL_HEALTH";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string InstrumentId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the instrument administration.</summary>
    [Id(7)] public MentalHealthState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(MentalHealthRecordedV1),
        InstrumentId,
        Snapshot.PatientId,
        Snapshot.InstrumentName,
        Snapshot.InstrumentDefId ?? string.Empty,
        Snapshot.AdministrationDateTime.ToString("O"),
        Snapshot.TotalScore?.ToString("G29") ?? string.Empty,
        Snapshot.ScoreInterpretation ?? string.Empty,
        Snapshot.IsPositiveScreen?.ToString() ?? string.Empty,
        Snapshot.AdministeredById ?? string.Empty,
        Snapshot.OrderingProviderId ?? string.Empty,
        Snapshot.LocationId ?? string.Empty,
        Snapshot.VisitId ?? string.Empty,
        Snapshot.Status,
        Snapshot.Comments ?? string.Empty);
}
