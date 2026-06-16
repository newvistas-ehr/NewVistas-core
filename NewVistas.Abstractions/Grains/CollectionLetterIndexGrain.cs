// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CollectionLetterIndexGrain : Grain, ICollectionLetterIndexGrain
{
    private readonly IPersistentState<CollectionLetterIndexState> _state;

    public CollectionLetterIndexGrain(
        [PersistentState("collectionLetterIndexState", "collectionLetterIndexStore")]
        IPersistentState<CollectionLetterIndexState> state)
    {
        _state = state;
    }

    public Task<List<CollectionLetterIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(CollectionLetterIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.LetterId == entry.LetterId);
        _state.State.Entries.Insert(0, entry); // newest first
        await _state.WriteStateAsync();
    }

    public Task<int> GetNextDunningSequenceAsync()
    {
        int max = _state.State.Entries.Count > 0
            ? _state.State.Entries.Max(e => e.DunningSequence)
            : 0;
        return Task.FromResult(max + 1);
    }

    public Task<List<CollectionLetterIndexEntry>> GetByStatusAsync(CollectionLetterStatus status)
        => Task.FromResult(
            _state.State.Entries.Where(e => e.Status == status).ToList());

    public Task<CollectionLetterIndexEntry?> GetLatestByTypeAsync(CollectionLetterType letterType)
        => Task.FromResult(
            _state.State.Entries.FirstOrDefault(e => e.LetterType == letterType));
}
