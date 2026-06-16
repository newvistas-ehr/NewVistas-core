// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Problems;

/// <summary>
/// Causal event recording the inactivation of a problem on the patient's
/// problem list (VistA PROBLEM LIST file #9000011 — GMPLSAVE inactivation).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemInactivatedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PROBLEMS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The problem ID being inactivated.</summary>
    [Id(6)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>Date the problem was resolved/inactivated.</summary>
    [Id(7)] public DateTime? DateResolved { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(ProblemInactivatedV1),
        ProblemId,
        DateResolved?.ToString("O") ?? string.Empty);
}
