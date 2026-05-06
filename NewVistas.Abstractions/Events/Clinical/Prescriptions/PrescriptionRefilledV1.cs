// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Events.Clinical.Prescriptions;

/// <summary>
/// Causal event recording a refill (subsequent dispense) of a prescription.
/// Mirrors VistA PRESCRIPTION REFILL sub-file (#52.1).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PrescriptionRefilledV1 : IClinicalEvent
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = "PRESCRIPTIONS";
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    [Id(6)] public string PrescriptionId { get; init; } = string.Empty;

    /// <summary>Sequential fill number — 0 = original fill, 1+ = refills.</summary>
    [Id(7)] public int FillNumber { get; init; }

    [Id(8)] public DateTime FillDate { get; init; }
    [Id(9)] public int? Quantity { get; init; }
    [Id(10)] public int? DaysSupply { get; init; }
    [Id(11)] public string? RxNumber { get; init; }

    /// <summary>Refills remaining after this dispense.</summary>
    [Id(12)] public int? RefillsRemainingAfter { get; init; }

    public string Canonicalize() => string.Join("|",
        nameof(PrescriptionRefilledV1),
        PrescriptionId,
        FillNumber.ToString(),
        FillDate.ToString("O"),
        Quantity?.ToString() ?? string.Empty,
        DaysSupply?.ToString() ?? string.Empty,
        RxNumber ?? string.Empty,
        RefillsRemainingAfter?.ToString() ?? string.Empty);
}
