// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Singleton directory of all nursing units — VistA File #210 list.
/// Grain key: "NURS-UNIT-IDX"
/// </summary>
[GenerateSerializer]
public class NursingUnitIndexState
{
    /// <summary>All registered nursing units, sorted alphabetically by name.</summary>
    [Id(0)] public List<NursingUnitEntry> Units { get; set; } = new();
}

/// <summary>Summary entry for a nursing unit in the system directory.</summary>
[GenerateSerializer]
public class NursingUnitEntry
{
    [Id(0)] public string UnitId { get; set; } = string.Empty;
    [Id(1)] public string UnitName { get; set; } = string.Empty;

    /// <summary>Unit specialty type (MedSurg, ICU, PCU, etc.).</summary>
    [Id(2)] public string? UnitType { get; set; }

    [Id(3)] public int TotalBeds { get; set; }
    [Id(4)] public int OccupiedBeds { get; set; }
}
