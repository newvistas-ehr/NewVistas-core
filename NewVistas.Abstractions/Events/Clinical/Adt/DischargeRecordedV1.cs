// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Adt;

/// <summary>
/// Causal event recording the discharge of a previously-admitted patient.
/// Recorded on the same admission movement grain.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DischargeRecordedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "ADT";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string MovementId { get; init; } = string.Empty;

    [Id(7)] public DateTime DischargeDateTime { get; init; }
    [Id(8)] public string? DischargeDiagnosis { get; init; }
    [Id(9)] public string? Disposition { get; init; }
    [Id(10)] public int? LengthOfStay { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(DischargeRecordedV1),
        MovementId,
        DischargeDateTime.ToString("O"),
        DischargeDiagnosis ?? string.Empty,
        Disposition ?? string.Empty,
        LengthOfStay?.ToString() ?? string.Empty);
}
