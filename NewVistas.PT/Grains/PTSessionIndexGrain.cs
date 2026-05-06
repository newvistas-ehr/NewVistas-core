// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Per-patient, per-body-group index of PT session grain keys.
/// Maintains entries sorted by SessionDate descending (most recent first).
/// Mirrors the PatientVitalIndexGrain sorted-insertion pattern.
/// </summary>
public class PTSessionIndexGrain : Grain, IPTSessionIndexGrain
{
    private readonly IPersistentState<PTSessionIndexState> _state;

    public PTSessionIndexGrain(
        [PersistentState("ptSessionIndexState", "physTherapySessionIndexStore")]
        IPersistentState<PTSessionIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            // Key format: "PTINDEX:{patientId}:{bodyGroup}"
            string key = this.GetPrimaryKeyString();
            string[] parts = key.Split(':');
            if (parts.Length >= 3)
            {
                _state.State.PatientId = parts[1];
                if (Enum.TryParse<BodyGroup>(parts[2], out BodyGroup bg))
                    _state.State.BodyGroup = bg;
            }
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddSessionKeyAsync(string sessionGrainKey, DateTime sessionDate, BodyGroup bodyGroup, Laterality side)
    {
        // Prevent duplicates
        if (_state.State.Entries.Any(e => e.SessionGrainKey == sessionGrainKey))
            return;

        PTSessionIndexEntry entry = new()
        {
            SessionGrainKey = sessionGrainKey,
            SessionDate = sessionDate,
            BodyGroup = bodyGroup,
            Side = side
        };

        // Insert in sorted position (descending by date)
        int insertIndex = _state.State.Entries.FindIndex(e => e.SessionDate <= sessionDate);
        if (insertIndex < 0)
            _state.State.Entries.Add(entry);
        else
            _state.State.Entries.Insert(insertIndex, entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveSessionKeyAsync(string sessionGrainKey)
    {
        int removed = _state.State.Entries.RemoveAll(e => e.SessionGrainKey == sessionGrainKey);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<PTSessionIndexEntry>> GetAllSessionsAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PTSessionIndexEntry>> GetLastNSessionsAsync(int count)
        => Task.FromResult(_state.State.Entries.Take(count).ToList());

    public Task<List<PTSessionIndexEntry>> GetSessionsByDateRangeAsync(DateTime from, DateTime to)
    {
        List<PTSessionIndexEntry> result = _state.State.Entries
            .Where(e => e.SessionDate >= from && e.SessionDate <= to)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}
