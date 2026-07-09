// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// One institution's capacity as seen by the system-wide view — the per-unit
/// rollups plus institution-level totals. Available is the only "placeable" number.
/// </summary>
[GenerateSerializer]
public class InstitutionCapacitySummary
{
    [Id(0)] public string InstitutionId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public InstitutionType Type { get; set; }
    [Id(3)] public string? HealthSystemId { get; set; }
    [Id(4)] public string? HealthSystemName { get; set; }
    [Id(5)] public bool AcceptsInboundTransfers { get; set; }
    [Id(6)] public HashSet<string> Capabilities { get; set; } = new();
    [Id(7)] public List<UnitCapacitySummary> Units { get; set; } = new();
    [Id(8)] public int TotalBeds { get; set; }
    [Id(9)] public int Available { get; set; }
    [Id(10)] public int Occupied { get; set; }
    [Id(11)] public int Dirty { get; set; }
    [Id(12)] public int Blocked { get; set; }
    [Id(13)] public int OutOfService { get; set; }
}

/// <summary>Point-in-time snapshot of capacity across institutions (fan-out read, not stored).</summary>
[GenerateSerializer]
public class SystemCapacitySnapshot
{
    [Id(0)] public DateTime AsOfUtc { get; set; }
    [Id(1)] public List<InstitutionCapacitySummary> Institutions { get; set; } = new();
}
