// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the ward location index grain.
/// Mirrors VistA WARD LOCATION file (#42).
/// </summary>
[GenerateSerializer]
public class WardLocationIndexState
{
    /// <summary>All ward locations.</summary>
    [Id(0)] public List<WardLocationEntry> Wards { get; set; } = new();

    /// <summary>Whether demo seed data has been loaded.</summary>
    [Id(1)] public bool IsSeeded { get; set; }
}

/// <summary>
/// A single ward location record.
/// VistA WARD LOCATION file (#42) — key fields: .01 NAME, 3 DIVISION, 7 AUTHORIZED BEDS
/// </summary>
[GenerateSerializer]
public class WardLocationEntry
{
    /// <summary>(.01) Internal ward identifier, e.g. "WARD-MED-3A".</summary>
    [Id(0)] public string WardId { get; set; } = string.Empty;

    /// <summary>(.01) Display name, e.g. "Medical Ward 3A".</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>Ward clinical type: MEDICINE, SURGERY, ICU, PSYCH, REHAB, OBS.</summary>
    [Id(2)] public string WardType { get; set; } = string.Empty;

    /// <summary>(3) Hospital division, e.g. "Main Campus".</summary>
    [Id(3)] public string? Division { get; set; }

    /// <summary>(7) Authorized bed capacity.</summary>
    [Id(4)] public int BedCapacity { get; set; }

    /// <summary>Whether this ward is currently active.</summary>
    [Id(5)] public bool IsActive { get; set; } = true;
}
