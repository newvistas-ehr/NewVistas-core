// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class AnesthesiaRecordIndexGrain : Grain, IAnesthesiaRecordIndexGrain
{
    private readonly IPersistentState<AnesthesiaRecordIndexState> _state;
    public AnesthesiaRecordIndexGrain(
        [PersistentState("anesthesiaRecordIndexState", "anesthesiaRecordIndexStore")]
        IPersistentState<AnesthesiaRecordIndexState> state) { _state = state; }

    public async Task AddOrUpdateAsync(AnesthesiaRecordIndexEntry entry)
    { _state.State.Entries[entry.RecordId] = entry; await _state.WriteStateAsync(); }

    public Task<List<AnesthesiaRecordIndexEntry>> GetByPatientAsync(string patientId) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.PatientId == patientId).OrderByDescending(e => e.CreatedDate).ToList());

    public Task<List<AnesthesiaRecordIndexEntry>> GetByAnesthesiologistAsync(string anesthesiologistId, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.AnesthesiologistId == anesthesiologistId).OrderByDescending(e => e.CreatedDate).Take(maxResults).ToList());

    public Task<List<AnesthesiaRecordIndexEntry>> GetByStatusAsync(string status, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.Where(e => e.Status == status).OrderByDescending(e => e.CreatedDate).Take(maxResults).ToList());

    public Task<List<AnesthesiaRecordIndexEntry>> SearchAsync(string? patientId, string? anesthesiologistId, string? status, string? anesthesiaType, int maxResults = 50)
    {
        IEnumerable<AnesthesiaRecordIndexEntry> q = _state.State.Entries.Values;
        if (!string.IsNullOrWhiteSpace(patientId)) q = q.Where(e => e.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(anesthesiologistId)) q = q.Where(e => e.AnesthesiologistId == anesthesiologistId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(anesthesiaType)) q = q.Where(e => e.AnesthesiaType == anesthesiaType);
        return Task.FromResult(q.OrderByDescending(e => e.CreatedDate).Take(maxResults).ToList());
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);
}
