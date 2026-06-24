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
/// Facility-level drug entry grain (VistA File #50).
/// Each instance represents one row in the local drug catalog.
/// Keyed by the drug's VistA IEN.
/// </summary>
public class DrugGrain : Grain, IDrugGrain
{
    private readonly IPersistentState<DrugState> _state;

    public DrugGrain(
        [PersistentState("drugState", "drugFileStore")]
        IPersistentState<DrugState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Ensure grain key is set on the state for new grains.
        if (string.IsNullOrEmpty(_state.State.Ien))
            _state.State.Ien = this.GetPrimaryKeyString();

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<DrugState> GetDrugAsync() =>
        Task.FromResult(_state.State);

    public async Task SaveDrugAsync(DrugState drug)
    {
        drug.Ien = this.GetPrimaryKeyString();
        drug.LastModifiedDate = DateTime.UtcNow;

        if (_state.State.CreatedDate == default)
            drug.CreatedDate = DateTime.UtcNow;
        else
            drug.CreatedDate = _state.State.CreatedDate;

        _state.State = drug;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync(DateTime inactivationDate)
    {
        _state.State.IsActive = false;
        _state.State.InactivationDate = inactivationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.InactivationDate = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddPossibleDosageAsync(PossibleDosage dosage)
    {
        bool exists = _state.State.PossibleDosages
            .Any(d => d.Dose == dosage.Dose && d.Unit == dosage.Unit);

        if (exists)
            return;

        _state.State.PossibleDosages.Add(dosage);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<DrugIngredient>> GetIngredientsAsync() =>
        Task.FromResult(_state.State.Ingredients);

    public Task<DrugClassInfo> GetDrugClassAsync()
    {
        DrugState s = _state.State;

        List<string> all = new();
        if (!string.IsNullOrEmpty(s.PrimaryDrugClassCode))
            all.Add(s.PrimaryDrugClassCode);

        foreach (string code in s.SecondaryDrugClassCodes)
        {
            if (!string.IsNullOrEmpty(code) && !all.Contains(code))
                all.Add(code);
        }

        return Task.FromResult(new DrugClassInfo
        {
            PrimaryDrugClassCode = s.PrimaryDrugClassCode,
            PrimaryDrugClassName = s.PrimaryDrugClassName,
            SecondaryDrugClassCodes = new List<string>(s.SecondaryDrugClassCodes),
            AllClassCodes = all
        });
    }
}
