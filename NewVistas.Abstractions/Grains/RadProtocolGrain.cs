// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class RadProtocolGrain : Grain, IRadProtocolGrain
{
    private readonly IPersistentState<RadProtocolState> _state;
    public RadProtocolGrain([PersistentState("radProtocolState", "radProtocolStore")] IPersistentState<RadProtocolState> state) { _state = state; }

    public Task<RadProtocolState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(string protocolName, string imagingType, string? bodyPart, string? description, string? parameters)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.ProtocolId = this.GetPrimaryKeyString();
        _state.State.ProtocolName = protocolName;
        _state.State.ImagingType = imagingType;
        _state.State.BodyPart = bodyPart;
        _state.State.Description = description;
        _state.State.Parameters = parameters;
        _state.State.IsActive = true;
        _state.State.CreatedDate = now;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
