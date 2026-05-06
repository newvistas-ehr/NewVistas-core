// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Orders;

/// <summary>
/// Causal event recording the discontinuation/cancellation of an order —
/// VistA ORWDXA ACTION="DC".
/// </summary>
[GenerateSerializer, Immutable]
public sealed record OrderDiscontinuedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "ORDERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string OrderId { get; init; } = string.Empty;

    /// <summary>UTC instant the discontinuation took effect.</summary>
    [Id(7)] public DateTime DiscontinuedDateTime { get; init; }

    /// <summary>Reason for discontinuation (free text or coded reason).</summary>
    [Id(8)] public string Reason { get; init; } = string.Empty;

    /// <summary>Provider IEN who discontinued the order, if recorded.</summary>
    [Id(9)] public string? DiscontinuedByProviderId { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(OrderDiscontinuedV1),
        OrderId,
        DiscontinuedDateTime.ToString("O"),
        Reason,
        DiscontinuedByProviderId ?? string.Empty);
}
