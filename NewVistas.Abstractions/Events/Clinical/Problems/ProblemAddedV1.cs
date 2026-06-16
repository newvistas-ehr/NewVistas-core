// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// Causal event recording the addition of a new problem to the patient's
/// problem list (VistA PROBLEM LIST file #9000011 — GMPLSAVE).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemAddedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The problem ID assigned by the originating command.</summary>
    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>
    /// Full snapshot of the problem entry as added. Carries enough payload to
    /// reconstruct the problem list entry without consulting any other source.
    /// </summary>
    [Id(7)] public ProblemEntry Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(ProblemAddedV1),
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
        Snapshot.Comments ?? string.Empty);
}
