// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Event Capture Encounter Index Grain — application-wide searchable encounter index.
/// Singleton key: "EC-ENCOUNTER-IDX".
/// Persists to "ecEncounterIndexStore".
/// </summary>
public class EventCaptureEncounterIndexGrain : Grain, IEventCaptureEncounterIndexGrain
{
    private readonly IPersistentState<EventCaptureIndexState> _state;

    public EventCaptureEncounterIndexGrain(
        [PersistentState("ecEncounterIndexState", "ecEncounterIndexStore")]
        IPersistentState<EventCaptureIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EventCaptureIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.EncounterId == entry.EncounterId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<EventCaptureIndexEntry>> SearchAsync(
        string? patientId,
        string? dssUnitId,
        string? providerId,
        EcEncounterStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        int maxResults)
    {
        IEnumerable<EventCaptureIndexEntry> query = _state.State.Entries;

        if (!string.IsNullOrWhiteSpace(patientId))
            query = query.Where(e => e.PatientId == patientId);

        if (!string.IsNullOrWhiteSpace(dssUnitId))
            query = query.Where(e => e.DssUnitId == dssUnitId);

        if (!string.IsNullOrWhiteSpace(providerId))
            query = query.Where(e => e.PrimaryProviderId == providerId);

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.EncounterDateTime >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.EncounterDateTime <= toDate.Value);

        List<EventCaptureIndexEntry> result = query
            .OrderByDescending(e => e.EncounterDateTime)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<List<EventCaptureIndexEntry>> GetByPatientAsync(string patientId, int maxResults)
    {
        List<EventCaptureIndexEntry> result = _state.State.Entries
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.EncounterDateTime)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<EventCaptureIndexEntry>> GetByDssUnitAsync(string dssUnitId, int maxResults)
    {
        List<EventCaptureIndexEntry> result = _state.State.Entries
            .Where(e => e.DssUnitId == dssUnitId)
            .OrderByDescending(e => e.EncounterDateTime)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(result);
    }
}
