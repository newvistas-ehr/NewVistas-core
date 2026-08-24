// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// One problem entry replaced another (ADR-006) — used when a revision is recorded as a new
/// entry rather than an edit in place, so both the old and new diagnoses stay independently
/// visible with their own onset dates.
///
/// Replay applies whichever half is present: the two problems are separate heads and, under
/// federation, one may have arrived while the other has not.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemSupersededV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The problem being replaced.</summary>
    [Id(6)] public string SupersededProblemId { get; init; } = string.Empty;

    /// <summary>The problem replacing it.</summary>
    [Id(7)] public string SupersedingProblemId { get; init; } = string.Empty;

    /// <summary>Why — same vocabulary as a revision.</summary>
    [Id(8)] public RevisionReason Reason { get; init; }

    /// <summary>The clinician's own words.</summary>
    [Id(9)] public string? Narrative { get; init; }

    /// <summary>When the supersession takes clinical effect, if not the event time.</summary>
    [Id(10)] public DateTime? EffectiveUtc { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ProblemSupersededV1),
        SupersededProblemId,
        SupersedingProblemId,
        ((int)Reason).ToString(),
        Narrative ?? string.Empty,
        EffectiveUtc?.ToString("O") ?? string.Empty);
}
