// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// A diagnosis was asserted, with the evidence it rests on and a stated certainty (ADR-006).
///
/// Supersedes <see cref="ProblemAddedV1"/> for new writes. The older event could not simply gain
/// the provenance fields: its <c>Canonicalize()</c> enumerates specific properties, so anything
/// added to the snapshot would sit <b>outside</b> the hash — a persisted certainty could be
/// flipped from Unconfirmed to Confirmed and <c>VerifyChainAsync()</c> would still pass.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemAssertedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The problem id assigned by the originating command.</summary>
    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the problem as asserted.</summary>
    [Id(7)] public ProblemEntry Snapshot { get; init; } = new();

    /// <summary>Certainty stated at assertion.</summary>
    [Id(8)] public ProblemVerificationStatus VerificationStatus { get; init; }

    /// <summary>
    /// What this assertion rests on, including <see cref="EvidencePolarity.NotAssessed"/> rows
    /// recording what was deliberately not checked.
    /// </summary>
    [Id(9)] public List<EvidenceRef> Evidence { get; init; } = new();

    /// <summary>
    /// The problem this assertion replaces, when it arose from splitting an earlier entry.
    /// </summary>
    [Id(10)] public string? SupersedesProblemId { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ProblemAssertedV1),
        ProblemId,
        Snapshot.DiagnosisCode ?? string.Empty,
        Snapshot.Diagnosis,
        Snapshot.Status,
        Snapshot.Condition ?? string.Empty,
        Snapshot.Priority ?? string.Empty,
        Snapshot.DateOfOnset?.ToString("O") ?? string.Empty,
        Snapshot.DateRecorded.ToString("O"),
        Snapshot.RecordingProviderId ?? string.Empty,
        Snapshot.ResponsibleProviderId ?? string.Empty,
        Snapshot.ClinicId ?? string.Empty,
        Snapshot.IsServiceConnected.ToString(),
        Snapshot.Comments ?? string.Empty,
        ((int)VerificationStatus).ToString(),
        EvidenceRef.CanonicalizeList(Evidence),
        SupersedesProblemId ?? string.Empty);
}
