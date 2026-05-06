// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class TBIScreeningIndexState
{
    [Id(0)] public List<TBIScreeningSummaryEntry> Screenings { get; set; } = new();
}

public class TBIScreeningIndexGrain : Grain, ITBIScreeningIndexGrain
{
    private readonly IPersistentState<TBIScreeningIndexState> _state;

    public TBIScreeningIndexGrain(
        [PersistentState("tbiScreeningIndexState", "tbiScreeningIndexStore")] IPersistentState<TBIScreeningIndexState> state)
    {
        _state = state;
    }

    public Task<List<TBIScreeningSummaryEntry>> GetAllScreeningsAsync() =>
        Task.FromResult(_state.State.Screenings
            .OrderByDescending(s => s.ScreeningDate)
            .ToList());

    public Task<List<TBIScreeningSummaryEntry>> GetPositiveScreeningsAsync() =>
        Task.FromResult(_state.State.Screenings
            .Where(s => s.Result == TBIScreeningResult.PositiveRequiresEvaluation)
            .OrderByDescending(s => s.ScreeningDate)
            .ToList());

    public async Task UpsertScreeningAsync(TBIScreeningSummaryEntry entry)
    {
        int idx = _state.State.Screenings.FindIndex(s => s.ScreeningId == entry.ScreeningId);
        if (idx >= 0)
            _state.State.Screenings[idx] = entry;
        else
            _state.State.Screenings.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveScreeningAsync(string screeningId)
    {
        int idx = _state.State.Screenings.FindIndex(s => s.ScreeningId == screeningId);
        if (idx >= 0)
        {
            _state.State.Screenings.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
