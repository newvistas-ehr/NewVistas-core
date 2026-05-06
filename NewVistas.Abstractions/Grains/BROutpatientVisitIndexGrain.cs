// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Outpatient Visit Index Grain — grain key: "BR-VISIT-IDX:{patientId}"
/// </summary>
public class BROutpatientVisitIndexGrain : Grain, IBROutpatientVisitIndexGrain
{
    private readonly IPersistentState<BROutpatientVisitIndexState> _state;

    public BROutpatientVisitIndexGrain(
        [PersistentState("brOutpatientVisitIndexState", "brOutpatientVisitIndexStore")]
        IPersistentState<BROutpatientVisitIndexState> state)
    {
        _state = state;
    }

    public Task<List<BROutpatientVisitIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Visits);

    public Task<List<BROutpatientVisitIndexEntry>> GetByDateRangeAsync(DateTime from, DateTime to)
        => Task.FromResult(_state.State.Visits
            .Where(v => v.VisitDate >= from && v.VisitDate <= to)
            .ToList());

    public async Task AddAsync(BROutpatientVisitIndexEntry entry)
    {
        _state.State.Visits.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string visitId, BRVisitStatus status)
    {
        BROutpatientVisitIndexEntry? entry = _state.State.Visits.FirstOrDefault(v => v.VisitId == visitId);
        if (entry is not null)
        {
            entry.Status = status;
            await _state.WriteStateAsync();
        }
    }
}
