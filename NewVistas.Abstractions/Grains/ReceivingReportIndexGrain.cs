// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ReceivingReportIndexGrain : Grain, IReceivingReportIndexGrain
{
    private readonly IPersistentState<ReceivingReportIndexState> _state;

    public ReceivingReportIndexGrain(
        [PersistentState("receivingReportIndexState", "ifcapReceivingReportIndexStore")]
        IPersistentState<ReceivingReportIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ReceivingReportIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.ReceivingReportId == entry.ReceivingReportId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ReceivingReportIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}
