// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// A problem should never have been recorded — wrong chart, duplicate keystroke (ADR-006).
///
/// Replaces <c>PatientGrain.RemoveProblemAsync</c>, which was a hard delete with no tombstone
/// and no event, leaving event replay and live state permanently disagreeing.
///
/// This is <b>not</b> the same as a diagnosis being disproved. A refuted diagnosis means the
/// patient genuinely was worked up and belongs in the denominator of "who was investigated for
/// this"; an entered-in-error row means nothing happened to this patient at all and must leave
/// both numerator and denominator. Treating them as one flag would make those opposite
/// treatments impossible.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemEnteredInErrorV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>
    /// Why this record is void. Required and non-empty — voiding a clinical record without a
    /// stated reason is indistinguishable from concealing one.
    /// </summary>
    [Id(7)] public string Reason { get; init; } = string.Empty;

    public string Canonicalize() => string.Join("|",
        nameof(ProblemEnteredInErrorV1),
        ProblemId,
        Reason);
}
