// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PharmacogenomicsGrain : Grain, IPharmacogenomicsGrain
{
    private readonly IPersistentState<PharmacogenomicsState> _state;

    public PharmacogenomicsGrain(
        [PersistentState("pharmacogenomicsState", "pharmacogenomicsStore")] IPersistentState<PharmacogenomicsState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordResultAsync(PgxResultEntry result)
    {
        if (string.IsNullOrEmpty(result.ResultId))
            result.ResultId = Guid.NewGuid().ToString();
        // Upsert by gene — one current result per gene.
        _state.State.Results.RemoveAll(r => string.Equals(r.Gene, result.Gene, StringComparison.OrdinalIgnoreCase));
        _state.State.Results.Add(result);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveResultAsync(string gene)
    {
        int removed = _state.State.Results.RemoveAll(r => string.Equals(r.Gene, gene, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<PharmacogenomicsState> GetAsync() => Task.FromResult(_state.State);
}
