// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Labs;

/// <summary>
/// Causal event recording verification of a lab result by a qualified
/// reviewer — VistA LRVER1 VERIFY workflow. Transitions status to Completed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LabResultVerifiedV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "LABS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string LabTestId { get; init; } = string.Empty;

    [Id(7)] public string VerifyingProviderId { get; init; } = string.Empty;
    [Id(8)] public string VerifyingProviderName { get; init; } = string.Empty;
    [Id(9)] public DateTime VerifiedDateTime { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(LabResultVerifiedV1),
        LabTestId,
        VerifyingProviderId,
        VerifyingProviderName,
        VerifiedDateTime.ToString("O"));
}
