// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// DSI Event Grain — records a single CDS intervention firing and clinician response.
/// </summary>
public class DsiEventGrain : Grain, IDsiEventGrain
{
    private readonly IPersistentState<DsiEventState> _state;

    public DsiEventGrain(
        [PersistentState("dsiEventState", "dsiEventStore")] IPersistentState<DsiEventState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EventId))
            _state.State.EventId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordFiringAsync(
        string interventionId,
        string interventionTitle,
        string interventionType,
        string patientId,
        string? userId,
        string recommendedAction,
        string severity,
        List<string> triggerEvidence,
        string sourceCitation)
    {
        _state.State.InterventionId = interventionId;
        _state.State.InterventionTitle = interventionTitle;
        _state.State.InterventionType = interventionType;
        _state.State.PatientId = patientId;
        _state.State.UserId = userId;
        _state.State.RecommendedAction = recommendedAction;
        _state.State.Severity = severity;
        _state.State.TriggerEvidence = triggerEvidence;
        _state.State.SourceCitation = sourceCitation;
        _state.State.FiredDate = DateTime.UtcNow;
        _state.State.UserResponse = "pending";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task RecordResponseAsync(string response, string? overrideReason)
    {
        _state.State.UserResponse = response;
        _state.State.OverrideReason = overrideReason;
        _state.State.ResponseDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<DsiEventState> GetEventAsync() => Task.FromResult(_state.State);
}

/// <summary>
/// DSI Event Index Grain — audit log of all CDS intervention firings.
/// </summary>
public class DsiEventIndexGrain : Grain, IDsiEventIndexGrain
{
    private readonly IPersistentState<DsiEventIndexState> _state;

    public DsiEventIndexGrain(
        [PersistentState("dsiEventIndexState", "dsiEventIndexStore")] IPersistentState<DsiEventIndexState> state)
    {
        _state = state;
    }

    public async Task AddEventAsync(DsiEventSummary summary)
    {
        _state.State.Events.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateResponseAsync(string eventId, string response)
    {
        DsiEventSummary? existing = _state.State.Events.FirstOrDefault(e => e.EventId == eventId);
        if (existing != null)
        {
            existing.UserResponse = response;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<DsiEventSummary>> GetAllEventsAsync()
        => Task.FromResult(_state.State.Events.OrderByDescending(e => e.FiredDate).ToList());

    public Task<List<DsiEventSummary>> GetEventsByPatientAsync(string patientId, int maxResults = 50)
        => Task.FromResult(_state.State.Events
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.FiredDate).Take(maxResults).ToList());

    public Task<List<DsiEventSummary>> GetEventsByInterventionAsync(string interventionId)
        => Task.FromResult(_state.State.Events
            .Where(e => e.InterventionId == interventionId)
            .OrderByDescending(e => e.FiredDate).ToList());

    public Task<List<DsiEventSummary>> GetPendingEventsAsync()
        => Task.FromResult(_state.State.Events
            .Where(e => e.UserResponse == "pending")
            .OrderByDescending(e => e.FiredDate).ToList());
}
