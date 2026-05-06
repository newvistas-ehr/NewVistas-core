// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Entry in the catastrophic disability reference index (VistA File #27.17 CATASTROPHIC DISABILITY).
/// </summary>
[GenerateSerializer]
public record CatastrophicDisabilityEntry
{
    /// <summary>Numeric or alphanumeric code identifying this disability category.</summary>
    [Id(0)] public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description of the catastrophic disability.</summary>
    [Id(1)] public string Description { get; init; } = string.Empty;

    /// <summary>Whether this entry is currently active.</summary>
    [Id(2)] public bool IsActive { get; init; } = true;
}

/// <summary>
/// Singleton index of catastrophic disability categories (VistA File #27.17).
/// Pre-seeded with representative VistA CD codes: TBI, SCI, Visual Impairment, etc.
/// </summary>
[GenerateSerializer]
public class CatastrophicDisabilityIndexState
{
    /// <summary>All catastrophic disability entries.</summary>
    [Id(0)] public List<CatastrophicDisabilityEntry> Entries { get; set; } = new();
}
