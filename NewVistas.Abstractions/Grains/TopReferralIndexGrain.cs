// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class TopReferralIndexGrain : Grain, ITopReferralIndexGrain
{
    private readonly IPersistentState<TopReferralIndexState> _state;

    public TopReferralIndexGrain(
        [PersistentState("topReferralIndexState", "topReferralIndexStore")]
        IPersistentState<TopReferralIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(TopReferralIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.ReferralId == entry.ReferralId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<TopReferralIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<TopReferralIndexEntry>> GetPendingAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == TopReferralStatus.Pending || e.Status == TopReferralStatus.Certified)
            .ToList());

    public Task<List<TopReferralIndexEntry>> GetByAccountAsync(string arAccountId)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.ARAccountId == arAccountId)
            .ToList());
}
