// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// A problem-list row was observed during import or migration (ADR-006).
///
/// This is the one honest event for pre-existing data, and its honesty is in what it does
/// <b>not</b> claim. <see cref="IClinicalEvent.OccurredUtc"/> is the migration run time, never
/// the row's claimed date, because back-dating would forge a hash-chained record with a
/// fabricated timestamp and actor. The row's own claimed date travels separately in
/// <see cref="ClaimedRecordedDate"/>, clearly labelled as a claim.
///
/// The assertion this event makes is only: <i>on this date we observed this row, which claims
/// the following.</i> It deliberately does not set a certainty, an assertion id, or a revision
/// number — many imported rows are copies of someone else's problem list, not assertions made
/// here, and pretending otherwise would put fabricated denominators into the statistics.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemBaselineImportedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>The row as observed.</summary>
    [Id(7)] public ProblemEntry Snapshot { get; init; } = new();

    /// <summary>Where it came from — e.g. "ZWR ^AUPNPROB", "HL7 ADT", "manual backfill".</summary>
    [Id(8)] public string BaselineSource { get; init; } = string.Empty;

    /// <summary>The recorded date the source row <i>claims</i>, kept distinct from when we saw it.</summary>
    [Id(9)] public DateTime? ClaimedRecordedDate { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ProblemBaselineImportedV1),
        ProblemId,
        Snapshot.DiagnosisCode ?? string.Empty,
        Snapshot.Diagnosis,
        Snapshot.Status,
        Snapshot.DateOfOnset?.ToString("O") ?? string.Empty,
        BaselineSource,
        ClaimedRecordedDate?.ToString("O") ?? string.Empty);
}
