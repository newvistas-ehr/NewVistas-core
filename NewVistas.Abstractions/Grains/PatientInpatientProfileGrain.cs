// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient inpatient medication profile index grain.
/// Grain key: "PSJ-PROFILE:{patientId}"
/// </summary>
public class PatientInpatientProfileGrain : Grain, IPatientInpatientProfileGrain
{
    private readonly IPersistentState<PatientInpatientProfileState> _state;

    public PatientInpatientProfileGrain(
        [PersistentState("inpatientProfileState", "inpatientProfileStore")]
        IPersistentState<PatientInpatientProfileState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateOrderAsync(InpatientOrderIndexEntry entry)
    {
        int existing = _state.State.Orders.FindIndex(o => o.OrderId == entry.OrderId);
        if (existing >= 0)
            _state.State.Orders[existing] = entry;
        else
            _state.State.Orders.Add(entry);

        _state.State.TotalOrders = _state.State.Orders.Count;
        await _state.WriteStateAsync();
    }

    public async Task RemoveOrderAsync(string orderId)
    {
        _state.State.Orders.RemoveAll(o => o.OrderId == orderId);
        _state.State.TotalOrders = _state.State.Orders.Count;
        await _state.WriteStateAsync();
    }

    public Task<List<InpatientOrderIndexEntry>> GetAllOrdersAsync() =>
        Task.FromResult(_state.State.Orders
            .OrderBy(StatusSortOrder)
            .ThenByDescending(o => o.StartDate)
            .ToList());

    public Task<List<InpatientOrderIndexEntry>> GetActiveOrdersAsync() =>
        Task.FromResult(_state.State.Orders
            .Where(o => o.Status == "ACTIVE")
            .OrderByDescending(o => o.StartDate)
            .ToList());

    public Task<List<InpatientOrderIndexEntry>> GetByTypeAsync(string orderType) =>
        Task.FromResult(_state.State.Orders
            .Where(o => o.OrderType == orderType)
            .OrderBy(StatusSortOrder)
            .ThenByDescending(o => o.StartDate)
            .ToList());

    public Task<List<InpatientOrderIndexEntry>> GetPendingVerificationAsync() =>
        Task.FromResult(_state.State.Orders
            .Where(o => !o.IsVerified && o.Status == "PENDING")
            .OrderByDescending(o => o.StartDate)
            .ToList());

    public Task<int> GetTotalCountAsync() => Task.FromResult(_state.State.TotalOrders);

    public async Task ClearAsync()
    {
        _state.State.Orders.Clear();
        _state.State.TotalOrders = 0;
        await _state.WriteStateAsync();
    }

    private static int StatusSortOrder(InpatientOrderIndexEntry o) => o.Status switch
    {
        "PENDING" => 0,
        "ACTIVE" => 1,
        "HOLD" => 2,
        "ON_CALL" => 3,
        "EXPIRED" => 4,
        "DISCONTINUED" => 5,
        _ => 6
    };
}
