// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// A diagnosis changed, with a coded reason (ADR-006).
///
/// Replaces the silent full-object overwrite in <c>PatientGrain.UpdateProblemAsync</c>, which
/// emitted nothing at all — so a diagnosis code could be changed from E11.9 to C34.90 and the
/// hash chain showed no trace of it.
///
/// The prior diagnosis is <b>denormalized onto this envelope</b> so one event answers "what
/// changed" without walking back through the stream. That matters under federation, where the
/// earlier envelope may not have arrived, and it is what makes the (from → to) pair countable.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemRevisedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>Full snapshot of the problem as it now stands.</summary>
    [Id(7)] public ProblemEntry Snapshot { get; init; } = new();

    /// <summary>Which revision this is, counting the original assertion as 1.</summary>
    [Id(8)] public int RevisionNumber { get; init; }

    /// <summary>
    /// Why it changed. <see cref="RevisionReason.Correction"/> is the only value that counts as
    /// a diagnostic error; see <see cref="RevisionSemantics"/>.
    /// </summary>
    [Id(9)] public RevisionReason Reason { get; init; }

    /// <summary>The clinician's own words about the change.</summary>
    [Id(10)] public string? Narrative { get; init; }

    /// <summary>Certainty after the revision.</summary>
    [Id(11)] public ProblemVerificationStatus VerificationStatus { get; init; }

    /// <summary>Evidence supporting the revised assertion. Replaces the prior list wholesale.</summary>
    [Id(12)] public List<EvidenceRef> Evidence { get; init; } = new();

    /// <summary>The diagnosis text before this revision.</summary>
    [Id(13)] public string PriorDiagnosis { get; init; } = string.Empty;

    /// <summary>The diagnosis code before this revision.</summary>
    [Id(14)] public string? PriorDiagnosisCode { get; init; }

    /// <summary>The certainty before this revision.</summary>
    [Id(15)] public ProblemVerificationStatus PriorVerificationStatus { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ProblemRevisedV1),
        ProblemId,
        Snapshot.DiagnosisCode ?? string.Empty,
        Snapshot.Diagnosis,
        Snapshot.Status,
        Snapshot.Condition ?? string.Empty,
        Snapshot.Priority ?? string.Empty,
        Snapshot.DateOfOnset?.ToString("O") ?? string.Empty,
        RevisionNumber.ToString(),
        ((int)Reason).ToString(),
        Narrative ?? string.Empty,
        ((int)VerificationStatus).ToString(),
        EvidenceRef.CanonicalizeList(Evidence),
        PriorDiagnosis,
        PriorDiagnosisCode ?? string.Empty,
        ((int)PriorVerificationStatus).ToString());
}
