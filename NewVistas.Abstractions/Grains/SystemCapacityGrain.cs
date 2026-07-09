// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Stateless system-wide capacity view: INSTITUTION-INDEX actives → fan-out over
/// each BED-CAPACITY:{id} rollup with Task.WhenAll. No persisted aggregate —
/// write-amplifying every bed event into a global cache buys nothing at ≤ dozens
/// of institutions. (Escape hatch documented on ISystemCapacityGrain.)
/// </summary>
[StatelessWorker]
public class SystemCapacityGrain : Grain, ISystemCapacityGrain
{
    public async Task<SystemCapacitySnapshot> GetSystemCapacityAsync(string? healthSystemId = null)
    {
        List<InstitutionCapacitySummary> institutions = await ReadInstitutionsAsync(healthSystemId);
        return new SystemCapacitySnapshot
        {
            AsOfUtc = DateTime.UtcNow,
            Institutions = institutions
        };
    }

    public async Task<List<InstitutionCapacitySummary>> FindPlacementTargetsAsync(
        BedType? requestedBedType, string? requiredCapability)
    {
        List<InstitutionCapacitySummary> institutions = await ReadInstitutionsAsync(null);
        return institutions
            .Where(i => i.AcceptsInboundTransfers)
            .Where(i => requiredCapability is null || i.Capabilities.Contains(requiredCapability))
            .Where(i => requestedBedType is null
                ? i.Available > 0
                : i.Units.Any(u => u.IsActive
                    && u.AvailableByType.TryGetValue(requestedBedType.Value.ToString(), out int n) && n > 0))
            .ToList();
    }

    private async Task<List<InstitutionCapacitySummary>> ReadInstitutionsAsync(string? healthSystemId)
    {
        IInstitutionIndexGrain index = GrainFactory.GetGrain<IInstitutionIndexGrain>("INSTITUTION-INDEX");
        List<InstitutionIndexEntry> entries = healthSystemId is null
            ? await index.GetAllAsync()
            : await index.GetByHealthSystemAsync(healthSystemId);

        var reads = entries.Select(async e =>
        {
            List<UnitCapacitySummary> units = await GrainFactory
                .GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{e.InstitutionId}")
                .GetUnitsAsync();
            return new InstitutionCapacitySummary
            {
                InstitutionId = e.InstitutionId,
                Name = e.Name,
                Type = e.Type,
                HealthSystemId = e.HealthSystemId,
                HealthSystemName = e.HealthSystemName,
                AcceptsInboundTransfers = e.AcceptsInboundTransfers,
                Capabilities = new HashSet<string>(e.Capabilities),
                Units = units,
                TotalBeds = units.Sum(u => u.TotalBeds),
                Available = units.Sum(u => u.Available),
                Occupied = units.Sum(u => u.Occupied),
                Dirty = units.Sum(u => u.Dirty),
                Blocked = units.Sum(u => u.Blocked),
                OutOfService = units.Sum(u => u.OutOfService)
            };
        }).ToList();

        return (await Task.WhenAll(reads)).ToList();
    }
}
