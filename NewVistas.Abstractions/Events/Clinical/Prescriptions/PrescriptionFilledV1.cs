// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Prescriptions;

/// <summary>
/// Causal event recording the original fill of a prescription — VistA
/// PSOORED.m fill workflow. Refills (subsequent dispenses) record
/// <see cref="PrescriptionRefilledV1"/> instead.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PrescriptionFilledV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PRESCRIPTIONS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string PrescriptionId { get; init; } = string.Empty;

    /// <summary>Date the fill was recorded (may be back-dated relative to <see cref="OccurredUtc"/>).</summary>
    [Id(7)] public DateTime FillDate { get; init; }

    [Id(8)] public int? Quantity { get; init; }
    [Id(9)] public int? DaysSupply { get; init; }
    [Id(10)] public string? RxNumber { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(PrescriptionFilledV1),
        PrescriptionId,
        FillDate.ToString("O"),
        Quantity?.ToString() ?? string.Empty,
        DaysSupply?.ToString() ?? string.Empty,
        RxNumber ?? string.Empty);
}
