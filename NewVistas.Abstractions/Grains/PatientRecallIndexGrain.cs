// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// System-level singleton index for patient recall entries.
/// Keyed by "SD-RECALL-IDX". Supports queries by clinic, patient, status, overdue, and date range.
/// </summary>
public class PatientRecallIndexGrain : Grain, IPatientRecallIndexGrain
{
    private readonly IPersistentState<PatientRecallIndexState> _state;

    public PatientRecallIndexGrain(
        [PersistentState("patientRecallIndexState", "patientRecallIndexStore")]
        IPersistentState<PatientRecallIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PatientRecallIndexEntry entry)
    {
        _state.State.Entries[entry.EntryId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string entryId)
    {
        _state.State.Entries.Remove(entryId);
        await _state.WriteStateAsync();
    }

    public Task<List<PatientRecallIndexEntry>> GetByClinicAsync(string clinicId, int maxResults = 50)
    {
        List<PatientRecallIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.ClinicId == clinicId)
            .OrderBy(e => e.RecallDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<PatientRecallIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50)
    {
        List<PatientRecallIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.RecallDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<PatientRecallIndexEntry>> GetByStatusAsync(string status, int maxResults = 50)
    {
        List<PatientRecallIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.Status == status)
            .OrderBy(e => e.RecallDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<PatientRecallIndexEntry>> GetOverdueAsync(int maxResults = 50)
    {
        List<PatientRecallIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.Status == "OVERDUE")
            .OrderBy(e => e.RecallDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<PatientRecallIndexEntry>> GetDueInRangeAsync(
        DateTime rangeStart, DateTime rangeEnd, int maxResults = 50)
    {
        List<PatientRecallIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.RecallDate >= rangeStart && e.RecallDate <= rangeEnd
                        && e.Status is "PENDING" or "LETTER_SENT" or "OVERDUE")
            .OrderBy(e => e.RecallDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<PatientRecallIndexEntry>> SearchAsync(
        string? clinicId, string? status, string? recallType, int maxResults = 50)
    {
        IEnumerable<PatientRecallIndexEntry> query = _state.State.Entries.Values;

        if (!string.IsNullOrWhiteSpace(clinicId))
            query = query.Where(e => e.ClinicId == clinicId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(recallType))
            query = query.Where(e => e.RecallType == recallType);

        List<PatientRecallIndexEntry> results = query
            .OrderBy(e => e.RecallDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}
