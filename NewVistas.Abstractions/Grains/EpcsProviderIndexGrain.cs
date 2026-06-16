// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EpcsProviderIndexGrain : Grain, IEpcsProviderIndexGrain
{
    private readonly IPersistentState<EpcsProviderIndexState> _state;

    public EpcsProviderIndexGrain(
        [PersistentState("epcsProviderIndexState", "epcsProviderIndexStore")]
        IPersistentState<EpcsProviderIndexState> state)
    {
        _state = state;
    }

    public Task<List<EpcsProviderIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<EpcsProviderIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.CredentialStatus == EpcsCredentialStatus.Active).ToList());

    public async Task UpsertAsync(EpcsProviderIndexEntry entry)
    {
        EpcsProviderIndexEntry? existing = _state.State.Entries
            .FirstOrDefault(e => e.CredentialId == entry.CredentialId);
        if (existing != null)
        {
            existing.ProviderName = entry.ProviderName;
            existing.DeaNumber = entry.DeaNumber;
            existing.CredentialStatus = entry.CredentialStatus;
            existing.IdentityProofingLevel = entry.IdentityProofingLevel;
        }
        else
        {
            _state.State.Entries.Add(entry);
        }
        await _state.WriteStateAsync();
    }
}
