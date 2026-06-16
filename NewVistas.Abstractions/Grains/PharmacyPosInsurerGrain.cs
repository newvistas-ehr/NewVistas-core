// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PharmacyPosInsurerGrain : Grain, IPharmacyPosInsurerGrain
{
    private readonly IPersistentState<PharmacyPosInsurerState> _state;

    public PharmacyPosInsurerGrain(
        [PersistentState("posInsurerState", "posInsurerStore")]
        IPersistentState<PharmacyPosInsurerState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InsurerId))
        {
            _state.State.InsurerId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PharmacyPosInsurerState> GetAsync() => Task.FromResult(_state.State);

    public async Task SaveAsync(
        string insurerName, string bin, string pcn, string ncpdpVersion,
        string? pharmacyNcpdpId, string? serviceProviderIdQualifier,
        string? planName, string? helpDeskPhone, bool isActive)
    {
        _state.State.InsurerName = insurerName;
        _state.State.Bin = bin;
        _state.State.Pcn = pcn;
        _state.State.NcpdpVersion = ncpdpVersion;
        _state.State.PharmacyNcpdpId = pharmacyNcpdpId;
        _state.State.ServiceProviderIdQualifier = serviceProviderIdQualifier;
        _state.State.PlanName = planName;
        _state.State.HelpDeskPhone = helpDeskPhone;
        _state.State.IsActive = isActive;
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
