// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class TreatingFacilityListGrain : Grain, ITreatingFacilityListGrain
{
    private readonly IPersistentState<TreatingFacilityListState> _state;

    public TreatingFacilityListGrain(
        [PersistentState("treatingFacilityListState", "treatingFacilityListStore")]
        IPersistentState<TreatingFacilityListState> state)
    {
        _state = state;
    }

    public Task<TreatingFacilityListState> GetAsync()
        => Task.FromResult(_state.State);

    public Task<List<TreatingFacilityEntry>> GetActiveFacilitiesAsync()
        => Task.FromResult(_state.State.Facilities.Where(f => f.IsActive).ToList());

    public async Task AddOrUpdateFacilityAsync(TreatingFacilityEntry facility)
    {
        int idx = _state.State.Facilities.FindIndex(f => f.FacilityId == facility.FacilityId);
        if (idx >= 0)
            _state.State.Facilities[idx] = facility;
        else
            _state.State.Facilities.Add(facility);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateFacilityAsync(string facilityId)
    {
        _state.State.Facilities = _state.State.Facilities
            .Select(f => f.FacilityId == facilityId
                ? f with { IsActive = false }
                : f)
            .ToList();

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPrimaryFacilityAsync(string facilityId, string facilityName)
    {
        _state.State.PrimaryFacilityId   = facilityId;
        _state.State.PrimaryFacilityName = facilityName;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
