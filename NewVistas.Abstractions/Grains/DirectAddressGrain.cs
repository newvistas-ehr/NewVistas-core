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
/// Direct Address Grain — manages a registered Direct address for S/MIME transport.
/// §170.315(h)(1) — Direct Project.
/// </summary>
public class DirectAddressGrain : Grain, IDirectAddressGrain
{
    private readonly IPersistentState<DirectAddressState> _state;

    public DirectAddressGrain(
        [PersistentState("directAddressState", "directAddressStore")] IPersistentState<DirectAddressState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.DirectAddress))
        {
            string key = this.GetPrimaryKeyString();
            int colonIdx = key.IndexOf(':');
            _state.State.DirectAddress = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SaveAddressAsync(DirectAddressState address)
    {
        _state.State.DirectAddress = address.DirectAddress;
        _state.State.DisplayName = address.DisplayName;
        _state.State.OwnerType = address.OwnerType;
        _state.State.OwnerId = address.OwnerId;
        _state.State.OrganizationName = address.OrganizationName;
        _state.State.CertificateThumbprint = address.CertificateThumbprint;
        _state.State.CertificateSubject = address.CertificateSubject;
        _state.State.CertificateExpiration = address.CertificateExpiration;
        _state.State.IsActive = address.IsActive;
        _state.State.HispDomain = address.HispDomain;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<DirectAddressState> GetAddressAsync() => Task.FromResult(_state.State);

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateCertificateAsync(string thumbprint, string subject, DateTime expiration)
    {
        _state.State.CertificateThumbprint = thumbprint;
        _state.State.CertificateSubject = subject;
        _state.State.CertificateExpiration = expiration;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Direct Address Index Grain — registry of all Direct addresses.
/// </summary>
public class DirectAddressIndexGrain : Grain, IDirectAddressIndexGrain
{
    private readonly IPersistentState<DirectAddressIndexState> _state;

    public DirectAddressIndexGrain(
        [PersistentState("directAddressIndexState", "directAddressIndexStore")] IPersistentState<DirectAddressIndexState> state)
    {
        _state = state;
    }

    public async Task AddAddressAsync(DirectAddressSummary summary)
    {
        _state.State.Addresses.RemoveAll(a => a.DirectAddress == summary.DirectAddress);
        _state.State.Addresses.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAddressAsync(string directAddress)
    {
        _state.State.Addresses.RemoveAll(a => a.DirectAddress == directAddress);
        await _state.WriteStateAsync();
    }

    public Task<List<DirectAddressSummary>> GetAllAddressesAsync()
        => Task.FromResult(_state.State.Addresses.ToList());

    public Task<List<DirectAddressSummary>> GetActiveAddressesAsync()
        => Task.FromResult(_state.State.Addresses.Where(a => a.IsActive).ToList());
}
