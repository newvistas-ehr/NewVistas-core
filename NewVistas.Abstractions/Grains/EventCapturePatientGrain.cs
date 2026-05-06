// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Event Capture Patient Grain — per-patient index of EC encounter IDs.
/// Persists to "ecPatientStore".
/// </summary>
public class EventCapturePatientGrain : Grain, IEventCapturePatientGrain
{
    private readonly IPersistentState<EventCapturePatientState> _state;

    public EventCapturePatientGrain(
        [PersistentState("ecPatientState", "ecPatientStore")]
        IPersistentState<EventCapturePatientState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<EventCapturePatientState> GetAsync() => Task.FromResult(_state.State);

    public async Task AddEncounterAsync(string encounterId, DateTime encounterDateTime)
    {
        if (_state.State.EncounterEntries.Any(e => e.EncounterId == encounterId))
            return;

        // Insert in reverse-chronological order
        EcPatientEncounterEntry entry = new()
        {
            EncounterId = encounterId,
            EncounterDateTime = encounterDateTime,
        };

        int idx = _state.State.EncounterEntries
            .FindIndex(e => e.EncounterDateTime <= encounterDateTime);

        if (idx < 0)
            _state.State.EncounterEntries.Add(entry);
        else
            _state.State.EncounterEntries.Insert(idx, entry);

        _state.State.TotalEncounters++;
        if (_state.State.LastEncounterDate == null || encounterDateTime > _state.State.LastEncounterDate)
            _state.State.LastEncounterDate = encounterDateTime;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<EcPatientEncounterEntry>> GetEncounterEntriesAsync(int maxResults)
    {
        List<EcPatientEncounterEntry> result = _state.State.EncounterEntries
            .Take(maxResults)
            .ToList();
        return Task.FromResult(result);
    }
}
