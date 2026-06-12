// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class QMIncidentIndexState
{
    [Id(0)] public List<QMIncidentIndexEntry> Incidents { get; set; } = new();
}

public class QMIncidentIndexGrain : Grain, IQMIncidentIndexGrain
{
    private readonly IPersistentState<QMIncidentIndexState> _state;

    public QMIncidentIndexGrain(
        [PersistentState("qmIncidentIndexState", "qmIncidentIndexStore")] IPersistentState<QMIncidentIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertIncidentAsync(QMIncidentIndexEntry entry)
    {
        QMIncidentIndexEntry? existing = _state.State.Incidents.Find(i => i.IncidentId == entry.IncidentId);
        if (existing is not null)
            _state.State.Incidents.Remove(existing);
        _state.State.Incidents.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<QMIncidentIndexEntry>> GetAllIncidentsAsync()
    {
        List<QMIncidentIndexEntry> result = _state.State.Incidents
            .OrderByDescending(i => i.OccurrenceDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMIncidentIndexEntry>> GetIncidentsBySeverityAsync(OccurrenceSeverity severity)
    {
        List<QMIncidentIndexEntry> result = _state.State.Incidents
            .Where(i => i.Severity == severity)
            .OrderByDescending(i => i.OccurrenceDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMIncidentIndexEntry>> GetIncidentsByStatusAsync(IncidentStatus status)
    {
        List<QMIncidentIndexEntry> result = _state.State.Incidents
            .Where(i => i.Status == status)
            .OrderByDescending(i => i.OccurrenceDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMIncidentIndexEntry>> GetIncidentsByPatientAsync(string patientId, int maxResults = 50)
    {
        List<QMIncidentIndexEntry> result = _state.State.Incidents
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.OccurrenceDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMIncidentIndexEntry>> GetIncidentsByCategoryAsync(OccurrenceCategory category)
    {
        List<QMIncidentIndexEntry> result = _state.State.Incidents
            .Where(i => i.Category == category)
            .OrderByDescending(i => i.OccurrenceDate)
            .ToList();
        return Task.FromResult(result);
    }
}
