// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class DentalTreatmentIndexGrain : Grain, IDentalTreatmentIndexGrain
{
    private readonly IPersistentState<DentalTreatmentIndexState> _state;

    public DentalTreatmentIndexGrain(
        [PersistentState("dentalTreatmentIndexState", "dentalTreatmentIndexStore")]
        IPersistentState<DentalTreatmentIndexState> state)
    {
        _state = state;
    }

    public Task<List<DentalTreatmentIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Treatments);

    public Task<List<DentalTreatmentIndexEntry>> GetByStatusAsync(DentalTreatmentStatus status)
        => Task.FromResult(_state.State.Treatments.Where(e => e.Status == status).ToList());

    public async Task AddEntryAsync(DentalTreatmentIndexEntry entry)
    {
        _state.State.Treatments.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(string treatmentId, DentalTreatmentStatus status)
    {
        DentalTreatmentIndexEntry? entry = _state.State.Treatments
            .FirstOrDefault(e => e.TreatmentId == treatmentId);
        if (entry == null) return;

        entry.Status = status;
        await _state.WriteStateAsync();
    }
}
