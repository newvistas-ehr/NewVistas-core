// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Orders;

/// <summary>
/// Causal event recording the electronic signature of an order — VistA
/// ORWDXA ACTION="ES". Distinct from <see cref="OrderReleasedV1"/> (a sign
/// frequently triggers a release, but they are separate state transitions).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record OrderSignedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "ORDERS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string OrderId { get; init; } = string.Empty;

    /// <summary>The electronic signature value applied to the order.</summary>
    [Id(7)] public string ElectronicSignature { get; init; } = string.Empty;

    /// <summary>UTC instant the signature was applied.</summary>
    [Id(8)] public DateTime SignatureDateTime { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(OrderSignedV1),
        OrderId,
        ElectronicSignature,
        SignatureDateTime.ToString("O"));
}
