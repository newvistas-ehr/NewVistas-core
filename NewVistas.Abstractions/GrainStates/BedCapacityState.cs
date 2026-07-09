// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Dirty/cleaning bed detail carried inside the per-unit rollup so the
/// institution-wide EVS worklist is ONE grain read — no unit fan-out, and it
/// can never disagree with unit truth for longer than one push.
/// </summary>
[GenerateSerializer]
public class DirtyBedEntry
{
    [Id(0)] public string BedId { get; set; } = string.Empty;
    [Id(1)] public string RoomId { get; set; } = string.Empty;

    /// <summary>Dirty or Cleaning.</summary>
    [Id(2)] public BedLifecycleState State { get; set; }
    [Id(3)] public DateTime? DirtySince { get; set; }

    /// <summary>EVS needs to know precautions before entering.</summary>
    [Id(4)] public BedIsolationType Isolation { get; set; }
}

/// <summary>
/// Compact per-unit rollup pushed by the unit grain after every mutation
/// (and re-pushed on activation, so a missed push self-heals). Doubles as the
/// institution's unit DIRECTORY entry — replaces the old WARD-LOCATION-INDEX
/// and NURS-UNIT-IDX. <see cref="Available"/> is the ONLY honest "placeable
/// now" number; Dirty/Blocked beds are free but not placeable.
/// </summary>
[GenerateSerializer]
public class UnitCapacitySummary
{
    [Id(0)] public string UnitId { get; set; } = string.Empty;
    [Id(1)] public string InstitutionId { get; set; } = string.Empty;
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? UnitType { get; set; }
    [Id(4)] public bool IsActive { get; set; }
    [Id(5)] public int TotalBeds { get; set; }
    [Id(6)] public int Available { get; set; }
    [Id(7)] public int Reserved { get; set; }
    [Id(8)] public int Occupied { get; set; }
    [Id(9)] public int Dirty { get; set; }
    [Id(10)] public int Cleaning { get; set; }
    [Id(11)] public int Blocked { get; set; }
    [Id(12)] public int OutOfService { get; set; }

    /// <summary>Census entries without a bed — ED-boarding pressure indicator.</summary>
    [Id(13)] public int Boarders { get; set; }

    /// <summary>Only Dirty/Cleaning beds — small and bounded; feeds the EVS queue.</summary>
    [Id(14)] public List<DirtyBedEntry> DirtyBeds { get; set; } = new();

    [Id(15)] public DateTime LastUpdated { get; set; }

    /// <summary>Placeable beds broken down by bed type (key = BedType name) — feeds transfer-target search.</summary>
    [Id(16)] public Dictionary<string, int> AvailableByType { get; set; } = new();

    /// <summary>Derived, never stored: beds that exist operationally today.</summary>
    public int OperationalBeds => TotalBeds - OutOfService - Blocked;
}

/// <summary>
/// Per-institution capacity board + unit directory. Grain key:
/// "BED-CAPACITY:{institutionId}". Read-mostly; written ONLY by unit-grain
/// pushes (one direction, eventual, cheap — never a second source of truth).
/// </summary>
[GenerateSerializer]
public class BedCapacityState
{
    [Id(0)] public string InstitutionId { get; set; } = string.Empty;
    [Id(1)] public List<UnitCapacitySummary> Units { get; set; } = new();
    [Id(2)] public DateTime LastModifiedDate { get; set; }
}
