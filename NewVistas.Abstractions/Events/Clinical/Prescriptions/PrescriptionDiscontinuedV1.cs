// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Prescriptions;

/// <summary>
/// Causal event recording the discontinuation of a prescription — VistA
/// PSO discontinue workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PrescriptionDiscontinuedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PRESCRIPTIONS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string PrescriptionId { get; init; } = string.Empty;

    /// <summary>Reason for discontinuation.</summary>
    [Id(7)] public string Reason { get; init; } = string.Empty;

    public string Canonicalize() => string.Join("|",
        nameof(PrescriptionDiscontinuedV1),
        PrescriptionId,
        Reason);
}
