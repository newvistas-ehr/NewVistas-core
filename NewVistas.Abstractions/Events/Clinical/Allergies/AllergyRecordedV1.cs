// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Allergies;

/// <summary>
/// Causal event recording the addition of a new allergy entry to the
/// patient's allergy list — VistA PATIENT ALLERGIES file (#120.8), GMR/GMRA
/// allergy entry workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AllergyRecordedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "ALLERGIES";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The allergy ID assigned by the originating command.</summary>
    [Id(6)] public string AllergyId { get; init; } = string.Empty;

    /// <summary>
    /// Full snapshot of the allergy entry as recorded. Reconstruction-complete
    /// — replay does not depend on any other source.
    /// </summary>
    [Id(7)] public AllergyEntry Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(AllergyRecordedV1),
        AllergyId,
        Snapshot.Allergen,
        Snapshot.AllergenType,
        Snapshot.AllergenId ?? string.Empty,
        Snapshot.ReactionType,
        string.Join(",", Snapshot.Reactions),
        Snapshot.Severity ?? string.Empty,
        Snapshot.ObservedHistorical ?? string.Empty,
        Snapshot.OriginatorId ?? string.Empty,
        Snapshot.OriginationDateTime.ToString("O"),
        Snapshot.Comments ?? string.Empty);
}
