// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MassCasualtyIncidentIndexGrain : Grain, IMassCasualtyIncidentIndexGrain
{
    private readonly IPersistentState<MassCasualtyIncidentIndexState> _state;
    public MassCasualtyIncidentIndexGrain(
        [PersistentState("mciIncidentIndexState", "mciIncidentIndexStore")]
        IPersistentState<MassCasualtyIncidentIndexState> state) { _state = state; }

    public async Task AddOrUpdateAsync(MassCasualtyIncidentIndexEntry entry)
    { _state.State.Entries[entry.IncidentId] = entry; await _state.WriteStateAsync(); }

    public Task<List<MassCasualtyIncidentIndexEntry>> GetAllAsync(int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.OrderByDescending(e => e.ActivatedDate).Take(maxResults).ToList());

    public Task<List<MassCasualtyIncidentIndexEntry>> GetActiveAsync() =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.Status == "ACTIVE").OrderByDescending(e => e.ActivatedDate).ToList());

    public Task<List<MassCasualtyIncidentIndexEntry>> GetByStatusAsync(string status, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.Status == status).OrderByDescending(e => e.ActivatedDate).Take(maxResults).ToList());

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);
}

public class MassCasualtyCasualtyIndexGrain : Grain, IMassCasualtyCasualtyIndexGrain
{
    private readonly IPersistentState<MassCasualtyCasualtyIndexState> _state;
    public MassCasualtyCasualtyIndexGrain(
        [PersistentState("mciCasualtyIndexState", "mciCasualtyIndexStore")]
        IPersistentState<MassCasualtyCasualtyIndexState> state) { _state = state; }

    public async Task AddOrUpdateAsync(MassCasualtyCasualtyIndexEntry entry)
    { _state.State.Entries[entry.CasualtyId] = entry; await _state.WriteStateAsync(); }

    public Task<List<MassCasualtyCasualtyIndexEntry>> GetByIncidentAsync(string incidentId) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.IncidentId == incidentId).OrderBy(e => e.RegisteredDate).ToList());

    public Task<List<MassCasualtyCasualtyIndexEntry>> GetByTriageCategoryAsync(string incidentId, string triageCategory) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.IncidentId == incidentId && e.TriageCategory == triageCategory).OrderBy(e => e.RegisteredDate).ToList());

    public Task<List<MassCasualtyCasualtyIndexEntry>> GetByTreatmentAreaAsync(string incidentId, string treatmentArea) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.IncidentId == incidentId && e.TreatmentArea == treatmentArea).OrderBy(e => e.RegisteredDate).ToList());

    public Task<List<MassCasualtyCasualtyIndexEntry>> SearchAsync(string? incidentId, string? triageCategory, string? disposition, int maxResults = 100)
    {
        IEnumerable<MassCasualtyCasualtyIndexEntry> q = _state.State.Entries.Values;
        if (!string.IsNullOrWhiteSpace(incidentId)) q = q.Where(e => e.IncidentId == incidentId);
        if (!string.IsNullOrWhiteSpace(triageCategory)) q = q.Where(e => e.TriageCategory == triageCategory);
        if (!string.IsNullOrWhiteSpace(disposition)) q = q.Where(e => e.Disposition == disposition);
        return Task.FromResult(q.OrderBy(e => e.RegisteredDate).Take(maxResults).ToList());
    }

    public Task<int> GetCountByIncidentAsync(string incidentId) =>
        Task.FromResult(_state.State.Entries.Values.Count(e => e.IncidentId == incidentId));
}
