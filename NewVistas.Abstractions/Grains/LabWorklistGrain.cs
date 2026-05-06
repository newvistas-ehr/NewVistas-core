// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class LabWorklistGrain : Grain, ILabWorklistGrain
{
    private readonly IPersistentState<LabWorklistState> _state;

    public LabWorklistGrain(
        [PersistentState("labWorklistState", "labWorklistStore")]
        IPersistentState<LabWorklistState> state)
    { _state = state; }

    public Task<LabWorklistState> GetAsync() => Task.FromResult(_state.State);

    public async Task RefreshAsync(List<LabWorklistItem> items)
    {
        _state.State.Items = items.OrderByDescending(i => i.IsCritical)
            .ThenBy(i => i.OrderedDate).ToList();
        _state.State.LastRefreshedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddItemAsync(LabWorklistItem item)
    {
        _state.State.Items.RemoveAll(i => i.LabTestId == item.LabTestId);
        _state.State.Items.Add(item);
        await _state.WriteStateAsync();
    }

    public async Task RemoveItemAsync(string labTestId)
    {
        _state.State.Items.RemoveAll(i => i.LabTestId == labTestId);
        await _state.WriteStateAsync();
    }

    public Task<List<LabWorklistItem>> GetByCategoryAsync(LabWorklistCategory category)
        => Task.FromResult(_state.State.Items.Where(i => i.WorklistCategory == category).ToList());

    public Task<List<LabWorklistItem>> GetCriticalAsync()
        => Task.FromResult(_state.State.Items.Where(i => i.IsCritical).ToList());
}
