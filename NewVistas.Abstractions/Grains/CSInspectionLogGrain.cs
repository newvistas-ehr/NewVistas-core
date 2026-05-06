// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class CSInspectionLogState
{
    [Id(0)] public List<CSInspectionSummaryEntry> Inspections { get; set; } = new();
}

public class CSInspectionLogGrain : Grain, ICSInspectionLogGrain
{
    private readonly IPersistentState<CSInspectionLogState> _state;

    public CSInspectionLogGrain(
        [PersistentState("csInspectionLogState", "csInspectionLogStore")] IPersistentState<CSInspectionLogState> state)
    {
        _state = state;
    }

    public Task<List<CSInspectionSummaryEntry>> GetAllInspectionsAsync() =>
        Task.FromResult(_state.State.Inspections
            .OrderByDescending(i => i.InspectionDateTime)
            .ToList());

    public Task<List<CSInspectionSummaryEntry>> GetInspectionsByTypeAsync(CSInspectionType type) =>
        Task.FromResult(_state.State.Inspections
            .Where(i => i.InspectionType == type)
            .OrderByDescending(i => i.InspectionDateTime)
            .ToList());

    public Task<List<CSInspectionSummaryEntry>> GetInspectionsByResultAsync(CSInspectionResult result) =>
        Task.FromResult(_state.State.Inspections
            .Where(i => i.OverallResult == result)
            .OrderByDescending(i => i.InspectionDateTime)
            .ToList());

    public Task<List<CSInspectionSummaryEntry>> GetFailedInspectionsAsync() =>
        Task.FromResult(_state.State.Inspections
            .Where(i => i.OverallResult == CSInspectionResult.Failed ||
                        i.OverallResult == CSInspectionResult.DiscrepancyIdentified)
            .OrderByDescending(i => i.InspectionDateTime)
            .ToList());

    public async Task UpsertInspectionAsync(CSInspectionSummaryEntry entry)
    {
        int idx = _state.State.Inspections.FindIndex(i => i.InspectionId == entry.InspectionId);
        if (idx >= 0)
            _state.State.Inspections[idx] = entry;
        else
            _state.State.Inspections.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveInspectionAsync(string inspectionId)
    {
        int idx = _state.State.Inspections.FindIndex(i => i.InspectionId == inspectionId);
        if (idx >= 0)
        {
            _state.State.Inspections.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
