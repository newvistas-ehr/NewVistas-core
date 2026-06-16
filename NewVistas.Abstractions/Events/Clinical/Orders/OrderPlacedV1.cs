// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Events.Clinical.Orders;

/// <summary>
/// Causal event recording the placement of a new order — VistA ORDER file
/// (#100), ORWDX SAVE / ORWDXA NEW.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record OrderPlacedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "ORDERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The order ID (grain key) of the new order.</summary>
    [Id(6)] public string OrderId { get; init; } = string.Empty;

    /// <summary>
    /// Full snapshot of the order as placed. Carries enough payload to
    /// reconstruct the order without consulting any other source.
    /// </summary>
    [Id(7)] public OrderState Snapshot { get; init; } = new();

    public string Canonicalize() => string.Join("|",
        nameof(OrderPlacedV1),
        OrderId,
        Snapshot.PatientId,
        Snapshot.OrderType,
        Snapshot.OrderableItem,
        Snapshot.OrderableItemId ?? string.Empty,
        Snapshot.ProviderId,
        Snapshot.ProviderName,
        Snapshot.StartDateTime.ToString("O"),
        Snapshot.LocationId ?? string.Empty,
        Snapshot.LocationName ?? string.Empty,
        Snapshot.Urgency,
        Snapshot.Instructions ?? string.Empty,
        Snapshot.Reason ?? string.Empty,
        Snapshot.Nature ?? string.Empty,
        Snapshot.Status,
        Snapshot.IsControlledSubstance.ToString());
}
