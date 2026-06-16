// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.MentalHealth;

/// <summary>
/// Causal event recording the clinician's risk-level assessment for a mental
/// health instrument administration. High legal salience: a documented risk
/// level on a screening establishes duty-of-care.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MentalHealthRiskAssessedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "MENTAL_HEALTH";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string InstrumentId { get; init; } = string.Empty;

    /// <summary>Risk level — 0=None, 1=Low, 2=Moderate, 3=High, 4=Imminent.</summary>
    [Id(7)] public int RiskLevel { get; init; }

    /// <summary>Clinician's narrative risk-assessment notes.</summary>
    [Id(8)] public string? RiskNotes { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(MentalHealthRiskAssessedV1),
        InstrumentId,
        RiskLevel.ToString(),
        RiskNotes ?? string.Empty);
}
