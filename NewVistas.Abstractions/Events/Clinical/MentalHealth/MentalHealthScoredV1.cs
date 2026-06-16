// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.MentalHealth;

/// <summary>
/// Causal event recording the (re-)scoring of a mental health instrument.
/// Captures the resulting score, interpretation, and screen positivity so a
/// replay can reconstruct the score state without consulting any other source.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MentalHealthScoredV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "MENTAL_HEALTH";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string InstrumentId { get; init; } = string.Empty;

    /// <summary>Computed total score.</summary>
    [Id(7)] public decimal TotalScore { get; init; }

    /// <summary>Score interpretation (e.g., NEGATIVE, MODERATE, SEVERE).</summary>
    [Id(8)] public string? ScoreInterpretation { get; init; }

    /// <summary>True if the screen scored positive per its threshold.</summary>
    [Id(9)] public bool? IsPositiveScreen { get; init; }

    /// <summary>How the score was computed — AUTO or MANUAL.</summary>
    [Id(10)] public string ScoringMethod { get; init; } = string.Empty;

    public string Canonicalize() => string.Join("|",
        nameof(MentalHealthScoredV1),
        InstrumentId,
        TotalScore.ToString("G29"),
        ScoreInterpretation ?? string.Empty,
        IsPositiveScreen?.ToString() ?? string.Empty,
        ScoringMethod);
}
