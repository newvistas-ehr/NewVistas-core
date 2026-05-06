// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Pharmacy Orderable Item grain (VistA File #50.7).
/// Each instance represents one orderable item used by CPRS during order entry.
/// Keyed by the orderable item's VistA IEN.
/// </summary>
public class PharmacyOrderableItemGrain : Grain, IPharmacyOrderableItemGrain
{
    private readonly IPersistentState<OrderableItemState> _state;

    public PharmacyOrderableItemGrain(
        [PersistentState("oiState", "orderableItemStore")]
        IPersistentState<OrderableItemState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Ien))
            _state.State.Ien = this.GetPrimaryKeyString();

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<OrderableItemState> GetItemAsync() =>
        Task.FromResult(_state.State);

    public async Task SaveItemAsync(OrderableItemState item)
    {
        item.Ien = this.GetPrimaryKeyString();
        item.LastModifiedDate = DateTime.UtcNow;

        if (_state.State.CreatedDate == default)
            item.CreatedDate = DateTime.UtcNow;
        else
            item.CreatedDate = _state.State.CreatedDate;

        _state.State = item;
        await _state.WriteStateAsync();
    }

    public async Task AddLinkedDrugAsync(string drugIen)
    {
        if (_state.State.LinkedDrugIens.Contains(drugIen))
            return;

        _state.State.LinkedDrugIens.Add(drugIen);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveLinkedDrugAsync(string drugIen)
    {
        if (!_state.State.LinkedDrugIens.Contains(drugIen))
            return;

        _state.State.LinkedDrugIens.Remove(drugIen);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddDosageAsync(OrderableDosage dosage)
    {
        bool exists = _state.State.AvailableDosages
            .Any(d => d.Dose == dosage.Dose && d.Unit == dosage.Unit && d.Route == dosage.Route);

        if (exists)
            return;

        _state.State.AvailableDosages.Add(dosage);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync(DateTime inactivationDate)
    {
        _state.State.IsActive = false;
        _state.State.InactivationDate = inactivationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
