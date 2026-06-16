// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Prescriptions;

/// <summary>
/// Causal event recording the creation of a new prescription — VistA
/// PRESCRIPTION file (#52), PSO new-Rx workflow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PrescriptionCreatedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PRESCRIPTIONS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string PrescriptionId { get; init; } = string.Empty;

    /// <summary>
    /// Full snapshot of the prescription as created. Reconstruction-complete
    /// — replay does not depend on any other source.
    /// </summary>
    [Id(7)] public PharmacyState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(PrescriptionCreatedV1),
        PrescriptionId,
        Snapshot.PatientId,
        Snapshot.DrugName,
        Snapshot.DrugId ?? string.Empty,
        Snapshot.Dosage ?? string.Empty,
        Snapshot.Route ?? string.Empty,
        Snapshot.Schedule ?? string.Empty,
        Snapshot.Sig ?? string.Empty,
        Snapshot.DaysSupply?.ToString() ?? string.Empty,
        Snapshot.Quantity?.ToString() ?? string.Empty,
        Snapshot.Refills?.ToString() ?? string.Empty,
        Snapshot.ProviderId ?? string.Empty,
        Snapshot.PharmacyId ?? string.Empty,
        Snapshot.OrderId ?? string.Empty,
        Snapshot.IsControlledSubstance.ToString(),
        Snapshot.DeaSchedule ?? string.Empty);
}
