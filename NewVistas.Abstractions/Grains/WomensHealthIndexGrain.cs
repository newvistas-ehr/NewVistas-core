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
/// Per-patient Women's Health notification index grain.
/// Key: "WH-IDX:{patientId}"
/// </summary>
public class WomensHealthIndexGrain : Grain, IWomensHealthIndexGrain
{
    private readonly IPersistentState<WomensHealthIndexState> _state;

    public WomensHealthIndexGrain(
        [PersistentState("womensHealthIndexState", "womensHealthIndexStore")]
        IPersistentState<WomensHealthIndexState> state)
    {
        _state = state;
    }

    public Task<List<WomensHealthIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<WomensHealthIndexEntry>> GetByTypeAsync(WomensHealthNotificationType notificationType)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.NotificationType == notificationType)
            .ToList());

    public Task<List<WomensHealthIndexEntry>> GetFollowUpRequiredAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.FollowUpRequired)
            .ToList());

    public async Task AddEntryAsync(WomensHealthIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(
        string notificationId,
        WomensHealthNotificationStatus status,
        bool? followUpRequired,
        DateTime? nextDueDate)
    {
        WomensHealthIndexEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.NotificationId == notificationId);
        if (entry != null)
        {
            entry.Status = status;
            if (followUpRequired.HasValue)
                entry.FollowUpRequired = followUpRequired.Value;
            if (nextDueDate.HasValue)
                entry.NextDueDate = nextDueDate;
            await _state.WriteStateAsync();
        }
    }
}
