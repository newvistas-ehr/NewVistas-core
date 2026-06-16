// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class IVAdmixOrderIndexState
{
    [Id(0)] public List<IVAdmixOrderIndexEntry> Orders { get; set; } = new();
}

public class IVAdmixOrderIndexGrain : Grain, IIVAdmixOrderIndexGrain
{
    private readonly IPersistentState<IVAdmixOrderIndexState> _state;

    public IVAdmixOrderIndexGrain(
        [PersistentState("ivAdmixOrderIndexState", "ivAdmixOrderIndexStore")] IPersistentState<IVAdmixOrderIndexState> state)
    {
        _state = state;
    }

    public Task<List<IVAdmixOrderIndexEntry>> GetAllOrdersAsync() =>
        Task.FromResult(_state.State.Orders
            .OrderByDescending(o => o.CreatedDate)
            .ToList());

    public Task<List<IVAdmixOrderIndexEntry>> GetPendingOrdersAsync() =>
        Task.FromResult(_state.State.Orders
            .Where(o => o.Status == IVAdmixOrderStatus.Pending || o.Status == IVAdmixOrderStatus.Verified)
            .OrderByDescending(o => o.CreatedDate)
            .ToList());

    public Task<List<IVAdmixOrderIndexEntry>> GetActiveOrdersAsync() =>
        Task.FromResult(_state.State.Orders
            .Where(o => o.Status == IVAdmixOrderStatus.Compounding || o.Status == IVAdmixOrderStatus.Ready)
            .OrderByDescending(o => o.CreatedDate)
            .ToList());

    public Task<List<IVAdmixOrderIndexEntry>> GetOrdersByStatusAsync(IVAdmixOrderStatus status) =>
        Task.FromResult(_state.State.Orders
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.CreatedDate)
            .ToList());

    public async Task UpsertOrderAsync(IVAdmixOrderIndexEntry entry)
    {
        int idx = _state.State.Orders.FindIndex(o => o.OrderId == entry.OrderId);
        if (idx >= 0)
            _state.State.Orders[idx] = entry;
        else
            _state.State.Orders.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveOrderAsync(string orderId)
    {
        int idx = _state.State.Orders.FindIndex(o => o.OrderId == orderId);
        if (idx >= 0)
        {
            _state.State.Orders.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
