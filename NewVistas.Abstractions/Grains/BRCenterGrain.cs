// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Training Center Grain — grain key: "BR-CENTER:{centerId}"
/// </summary>
public class BRCenterGrain : Grain, IBRCenterGrain
{
    private readonly IPersistentState<BRCenterState> _state;

    public BRCenterGrain(
        [PersistentState("brCenterState", "brCenterStore")]
        IPersistentState<BRCenterState> state)
    {
        _state = state;
    }

    public Task<BRCenterState> GetAsync() => Task.FromResult(_state.State);

    public async Task SaveAsync(
        string centerId,
        string name,
        string facilityCode,
        string city,
        string state,
        BRCenterType centerType,
        int bedCapacity,
        bool acceptingPatients,
        List<BRTrainingArea> programsOffered,
        string? phoneNumber,
        string? contactName,
        string? notes)
    {
        bool isNew = string.IsNullOrEmpty(_state.State.CenterId);
        _state.State.CenterId = centerId;
        _state.State.Name = name;
        _state.State.FacilityCode = facilityCode;
        _state.State.City = city;
        _state.State.State = state;
        _state.State.CenterType = centerType;
        _state.State.BedCapacity = bedCapacity;
        _state.State.AcceptingPatients = acceptingPatients;
        _state.State.ProgramsOffered = programsOffered;
        _state.State.PhoneNumber = phoneNumber;
        _state.State.ContactName = contactName;
        _state.State.Notes = notes;
        if (isNew) _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetAcceptingPatientsAsync(bool accepting)
    {
        _state.State.AcceptingPatients = accepting;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
