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
/// Institution (File #4). Register/update push the directory entry to the
/// INSTITUTION-INDEX so anchor and index never drift (the same orchestration
/// pattern PersonGrain uses for its index).
/// </summary>
public class InstitutionGrain : Grain, IInstitutionGrain
{
    private readonly IPersistentState<InstitutionState> _state;

    public InstitutionGrain(
        [PersistentState("institution", "institutionStore")]
        IPersistentState<InstitutionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstitutionId))
        {
            string rawKey = this.GetPrimaryKeyString();
            _state.State.InstitutionId = rawKey.StartsWith("INST:")
                ? rawKey["INST:".Length..]
                : rawKey;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<InstitutionState> GetAsync() => Task.FromResult(_state.State);

    public async Task RegisterAsync(string name, InstitutionType type, string? stationNumber,
        string? healthSystemId, string? healthSystemName,
        string? streetAddress, string? city, string? state, string? zip, string? phone,
        IEnumerable<string>? capabilities, IEnumerable<string>? legacyAliases)
    {
        // Idempotent — an already-registered institution is a no-op.
        if (!string.IsNullOrEmpty(_state.State.Name))
            return;

        _state.State.Name = name;
        _state.State.Type = type;
        _state.State.StationNumber = stationNumber;
        _state.State.HealthSystemId = healthSystemId;
        _state.State.HealthSystemName = healthSystemName;
        _state.State.StreetAddress = streetAddress;
        _state.State.City = city;
        _state.State.State = state;
        _state.State.Zip = zip;
        _state.State.Phone = phone;
        _state.State.IsActive = true;
        if (capabilities is not null)
            _state.State.Capabilities = new HashSet<string>(capabilities);
        if (legacyAliases is not null)
            _state.State.LegacyFacilityAliases = legacyAliases.ToList();

        await SaveAndIndexAsync();
    }

    public async Task UpdateAsync(string? name, InstitutionType? type, string? stationNumber,
        string? healthSystemId, string? healthSystemName,
        string? streetAddress, string? city, string? state, string? zip, string? phone)
    {
        if (name is not null) _state.State.Name = name;
        if (type is not null) _state.State.Type = type.Value;
        if (stationNumber is not null) _state.State.StationNumber = stationNumber;
        if (healthSystemId is not null) _state.State.HealthSystemId = healthSystemId;
        if (healthSystemName is not null) _state.State.HealthSystemName = healthSystemName;
        if (streetAddress is not null) _state.State.StreetAddress = streetAddress;
        if (city is not null) _state.State.City = city;
        if (state is not null) _state.State.State = state;
        if (zip is not null) _state.State.Zip = zip;
        if (phone is not null) _state.State.Phone = phone;
        await SaveAndIndexAsync();
    }

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive = isActive;
        await SaveAndIndexAsync();
    }

    public async Task SetCapabilitiesAsync(HashSet<string> capabilities)
    {
        _state.State.Capabilities = capabilities;
        await SaveAndIndexAsync();
    }

    public async Task SetAcceptsInboundTransfersAsync(bool accepts)
    {
        _state.State.AcceptsInboundTransfers = accepts;
        await SaveAndIndexAsync();
    }

    private async Task SaveAndIndexAsync()
    {
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        await GrainFactory.GetGrain<IInstitutionIndexGrain>("INSTITUTION-INDEX")
            .AddOrUpdateAsync(new InstitutionIndexEntry
            {
                InstitutionId = _state.State.InstitutionId,
                Name = _state.State.Name,
                Type = _state.State.Type,
                HealthSystemId = _state.State.HealthSystemId,
                HealthSystemName = _state.State.HealthSystemName,
                City = _state.State.City,
                State = _state.State.State,
                IsActive = _state.State.IsActive,
                AcceptsInboundTransfers = _state.State.AcceptsInboundTransfers,
                Capabilities = new HashSet<string>(_state.State.Capabilities)
            }, _state.State.LegacyFacilityAliases);
    }
}
