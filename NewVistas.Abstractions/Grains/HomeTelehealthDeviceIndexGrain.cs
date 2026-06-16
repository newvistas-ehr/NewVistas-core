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
/// System-wide device inventory index for Home Telehealth.
/// Grain key: "HT-DEVICE-IDX" (singleton)
/// </summary>
public class HomeTelehealthDeviceIndexGrain : Grain, IHomeTelehealthDeviceIndexGrain
{
    private readonly IPersistentState<HomeTelehealthDeviceIndexState> _state;

    public HomeTelehealthDeviceIndexGrain(
        [PersistentState("htDeviceIndexState", "htDeviceIndexStore")] IPersistentState<HomeTelehealthDeviceIndexState> state)
    {
        _state = state;
    }

    public async Task AddAsync(HtDeviceIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.DeviceId == entry.DeviceId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<HtDeviceIndexEntry>> GetAsync(HtDeviceType? deviceType, HtDeviceStatus? status)
    {
        IEnumerable<HtDeviceIndexEntry> query = _state.State.Entries;

        if (deviceType.HasValue)
            query = query.Where(e => e.DeviceType == deviceType.Value);

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        return Task.FromResult(query.OrderBy(e => e.DeviceName).ToList());
    }

    public async Task UpdateStatusAsync(string deviceId, HtDeviceStatus status, string? assignedPatientId)
    {
        HtDeviceIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.DeviceId == deviceId);
        if (entry != null)
        {
            entry.Status = status;
            entry.AssignedPatientId = assignedPatientId;
            await _state.WriteStateAsync();
        }
    }
}
