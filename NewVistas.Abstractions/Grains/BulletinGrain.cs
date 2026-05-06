// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class BulletinGrain : Grain, IBulletinGrain
{
    private readonly IPersistentState<BulletinState> _state;

    public BulletinGrain(
        [PersistentState("bulletinState", "bulletinStore")]
        IPersistentState<BulletinState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.BulletinName))
        {
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<BulletinState> GetBulletinAsync() => Task.FromResult(_state.State);

    public async Task CreateBulletinAsync(
        string bulletinName,
        string description,
        string associatedMailGroup,
        string triggerEvent,
        string? messageTemplate)
    {
        _state.State.BulletinName = bulletinName;
        _state.State.Description = description;
        _state.State.AssociatedMailGroup = associatedMailGroup;
        _state.State.TriggerEvent = triggerEvent;
        _state.State.MessageTemplate = messageTemplate;
        _state.State.IsActive = true;
        _state.State.FireCount = 0;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FireBulletinAsync()
    {
        _state.State.FireCount++;
        _state.State.LastFiredDateTime = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateTemplateAsync(string messageTemplate)
    {
        _state.State.MessageTemplate = messageTemplate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
