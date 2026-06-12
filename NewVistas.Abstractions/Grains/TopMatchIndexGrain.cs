// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class TopMatchIndexGrain : Grain, ITopMatchIndexGrain
{
    private readonly IPersistentState<TopMatchIndexState> _state;

    public TopMatchIndexGrain(
        [PersistentState("topMatchIndexState", "topMatchIndexStore")]
        IPersistentState<TopMatchIndexState> state)
    {
        _state = state;
    }

    public Task<List<TopMatchIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(TopMatchIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.MatchId == entry.MatchId);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<List<TopMatchIndexEntry>> GetPendingAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == TopMatchStatus.PendingMatch).ToList());

    public Task<List<TopMatchIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50)
        => Task.FromResult(_state.State.Entries.Where(e => e.MatchedPatientId == patientId)
            .OrderByDescending(e => e.OffsetReceivedDate).Take(maxResults).ToList());
}
