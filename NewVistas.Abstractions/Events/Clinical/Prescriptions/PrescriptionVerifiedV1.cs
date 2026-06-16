// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Prescriptions;

/// <summary>
/// Causal event recording pharmacist verification of a prescription —
/// VistA RPh verification step.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PrescriptionVerifiedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PRESCRIPTIONS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string PrescriptionId { get; init; } = string.Empty;

    /// <summary>Pharmacist who performed the verification.</summary>
    [Id(7)] public string PharmacistId { get; init; } = string.Empty;

    /// <summary>UTC instant verification was recorded.</summary>
    [Id(8)] public DateTime VerifiedDate { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(PrescriptionVerifiedV1),
        PrescriptionId,
        PharmacistId,
        VerifiedDate.ToString("O"));
}
