// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Index state for the engineering work order singleton index grain.
/// </summary>
[GenerateSerializer]
public class EngineeringWorkOrderIndexState
{
    [Id(0)]
    public List<WorkOrderIndexEntry> Entries { get; set; } = new();
}

/// <summary>
/// Implementation of <see cref="IEngineeringWorkOrderIndexGrain"/>.
/// Singleton grain that maintains a searchable index of all work orders.
/// </summary>
public class EngineeringWorkOrderIndexGrain : Grain, IEngineeringWorkOrderIndexGrain
{
    private readonly IPersistentState<EngineeringWorkOrderIndexState> _state;

    public EngineeringWorkOrderIndexGrain(
        [PersistentState("engWorkOrderIndexState", "engWorkOrderIndexStore")]
        IPersistentState<EngineeringWorkOrderIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(WorkOrderIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.WorkOrderId == entry.WorkOrderId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<WorkOrderIndexEntry>> SearchAsync(
        string? facilityId,
        EngineeringShop? shop,
        WorkOrderStatus? status,
        WorkOrderPriority? priority,
        WorkOrderType? workOrderType,
        string? assignedToId,
        DateTime? fromDate,
        DateTime? toDate,
        int maxResults)
    {
        IEnumerable<WorkOrderIndexEntry> query = _state.State.Entries;

        if (!string.IsNullOrWhiteSpace(facilityId))
            query = query.Where(e => e.FacilityId == facilityId);

        if (shop.HasValue)
            query = query.Where(e => e.Shop == shop.Value);

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(e => e.Priority == priority.Value);

        if (workOrderType.HasValue)
            query = query.Where(e => e.WorkOrderType == workOrderType.Value);

        if (!string.IsNullOrWhiteSpace(assignedToId))
            query = query.Where(e => e.AssignedToName != null);

        if (fromDate.HasValue)
            query = query.Where(e => e.CreatedDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.CreatedDate < toDate.Value);

        List<WorkOrderIndexEntry> results = query
            .OrderByDescending(e => e.CreatedDate)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<WorkOrderIndexEntry>> GetActiveAsync(int maxResults)
    {
        List<WorkOrderIndexEntry> results = _state.State.Entries
            .Where(e => e.Status == WorkOrderStatus.Open || e.Status == WorkOrderStatus.InProgress)
            .OrderByDescending(e => (int)e.Priority)
            .ThenBy(e => e.CreatedDate)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<WorkOrderIndexEntry>> GetByFacilityAsync(string facilityId, int maxResults)
    {
        List<WorkOrderIndexEntry> results = _state.State.Entries
            .Where(e => e.FacilityId == facilityId)
            .OrderByDescending(e => e.CreatedDate)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }
}
