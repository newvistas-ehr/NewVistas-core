// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient index grain for interaction screenings.
/// Key format: IXSCREEN-IDX:{patientId}
/// </summary>
public class InteractionScreeningIndexGrain : Grain, IInteractionScreeningIndexGrain
{
    private readonly IPersistentState<InteractionScreeningIndexState> _state;

    public InteractionScreeningIndexGrain(
        [PersistentState("interactionScreeningIndexState", "interactionScreeningIndexStore")]
        IPersistentState<InteractionScreeningIndexState> state)
    {
        _state = state;
    }

    public Task<List<InteractionScreeningIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<InteractionScreeningIndexEntry>> GetBlockedAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == InteractionScreeningStatus.BlockedPendingOverride)
            .ToList());

    public Task<InteractionScreeningIndexEntry?> GetByPrescriptionAsync(string prescriptionId)
        => Task.FromResult(_state.State.Entries.FirstOrDefault(e => e.PrescriptionId == prescriptionId));

    public async Task AddEntryAsync(InteractionScreeningIndexEntry entry)
    {
        // Replace existing entry for same prescription if re-screened
        _state.State.Entries.RemoveAll(e => e.PrescriptionId == entry.PrescriptionId);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryAsync(
        string screeningId,
        InteractionScreeningStatus status,
        int blockingCount,
        int totalInteractionCount)
    {
        InteractionScreeningIndexEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.ScreeningId == screeningId);

        if (entry is not null)
        {
            entry.Status = status;
            entry.BlockingCount = blockingCount;
            entry.TotalInteractionCount = totalInteractionCount;
            await _state.WriteStateAsync();
        }
    }
}
