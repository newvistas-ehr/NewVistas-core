// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PeriodontalChartIndexGrain : Grain, IPeriodontalChartIndexGrain
{
    private readonly IPersistentState<PeriodontalChartIndexState> _state;
    public PeriodontalChartIndexGrain(
        [PersistentState("periodontalChartIndexState", "periodontalChartIndexStore")]
        IPersistentState<PeriodontalChartIndexState> state) { _state = state; }

    public async Task AddOrUpdateAsync(PeriodontalChartIndexEntry entry)
    { _state.State.Entries[entry.ChartId] = entry; await _state.WriteStateAsync(); }

    public Task<List<PeriodontalChartIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.PatientId == patientId).OrderByDescending(e => e.ExamDate).Take(maxResults).ToList());

    public Task<List<PeriodontalChartIndexEntry>> GetByProviderAsync(string providerId, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.ProviderId == providerId).OrderByDescending(e => e.ExamDate).Take(maxResults).ToList());

    public Task<List<PeriodontalChartIndexEntry>> GetByClassificationAsync(string classification, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.Classification == classification).OrderByDescending(e => e.ExamDate).Take(maxResults).ToList());

    public Task<List<PeriodontalChartIndexEntry>> SearchAsync(string? patientId, string? providerId, string? status, int maxResults = 50)
    {
        IEnumerable<PeriodontalChartIndexEntry> q = _state.State.Entries.Values;
        if (!string.IsNullOrWhiteSpace(patientId)) q = q.Where(e => e.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(providerId)) q = q.Where(e => e.ProviderId == providerId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        return Task.FromResult(q.OrderByDescending(e => e.ExamDate).Take(maxResults).ToList());
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);
}
