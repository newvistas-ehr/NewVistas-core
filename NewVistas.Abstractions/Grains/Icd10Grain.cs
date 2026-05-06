// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// ICD-10-CM Diagnosis Code Grain — stores a single entry from VistA
/// ICD DIAGNOSIS file (#80) with coding system 10D.
///
/// Grain Key: The ICD-10-CM code (e.g., "A00.0", "M54.5").
/// </summary>
public class Icd10Grain : Grain, IIcd10Grain
{
    private readonly IPersistentState<Icd10State> _state;

    public Icd10Grain(
        [PersistentState("icd10State", "icd10Store")]
        IPersistentState<Icd10State> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Ien))
        {
            _state.State.Ien = this.GetPrimaryKeyString();
            _state.State.Code = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<Icd10State> GetCodeAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task LoadCodeAsync(
        string code,
        string shortDescription,
        string longDescription,
        bool isBillable,
        int orderNumber,
        string? chapter)
    {
        _state.State.Code = code;
        _state.State.ShortDescription = shortDescription;
        _state.State.LongDescription = longDescription;
        _state.State.IsBillable = isBillable;
        _state.State.OrderNumber = orderNumber;
        _state.State.Chapter = chapter;
        _state.State.CodingSystem = "10D";
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.IsActive);
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
