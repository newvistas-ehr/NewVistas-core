// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Scheduling;

/// <summary>Causal event recording check-in for a scheduled appointment.</summary>
[GenerateSerializer, Immutable]
public sealed record AppointmentCheckedInV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "SCHEDULING";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string AppointmentId { get; init; } = string.Empty;
    [Id(7)] public DateTime CheckInDateTime { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(AppointmentCheckedInV1),
        AppointmentId,
        CheckInDateTime.ToString("O"));
}
