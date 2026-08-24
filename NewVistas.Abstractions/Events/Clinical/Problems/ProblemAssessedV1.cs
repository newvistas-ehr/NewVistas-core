// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// New evidence arrived about an existing diagnosis without changing the diagnosis itself —
/// the workup proceeding (ADR-006).
///
/// This is deliberately <b>not</b> a revision: it may move certainty from Provisional to
/// Confirmed, or record a negative result that leaves the diagnosis standing, and neither is a
/// clinician being wrong. Replay must not touch <c>RevisionNumber</c> for this event.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemAssessedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>
    /// Evidence to append. Appended rather than replaced, and deduped on replay by
    /// (kind, source, code) — <c>RecentEventIds</c> caps at 1000, so a long replay would
    /// otherwise re-apply a duplicate beyond that window and double-count the citation.
    /// </summary>
    [Id(7)] public List<EvidenceRef> Evidence { get; init; } = new();

    /// <summary>Certainty after taking the new evidence into account.</summary>
    [Id(8)] public ProblemVerificationStatus VerificationStatus { get; init; }

    /// <summary>Certainty before it, so the movement is legible from this envelope alone.</summary>
    [Id(9)] public ProblemVerificationStatus PriorVerificationStatus { get; init; }

    /// <summary>Optional clinician narrative.</summary>
    [Id(10)] public string? Narrative { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ProblemAssessedV1),
        ProblemId,
        EvidenceRef.CanonicalizeList(Evidence),
        ((int)VerificationStatus).ToString(),
        ((int)PriorVerificationStatus).ToString(),
        Narrative ?? string.Empty);
}
