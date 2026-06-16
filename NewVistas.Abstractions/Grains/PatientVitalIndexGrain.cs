// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient index of all vital measurement grain keys.
/// Maintains entries sorted by DateTimeTaken descending (most recent first).
/// Supports date-range queries and type filtering for the "load more" pattern.
/// </summary>
public class PatientVitalIndexGrain : Grain, IPatientVitalIndexGrain
{
    private readonly IPersistentState<PatientVitalIndexState> _state;

    public PatientVitalIndexGrain(
        [PersistentState("patientVitalIndexState", "patientVitalIndexStore")]
        IPersistentState<PatientVitalIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddVitalKeyAsync(string vitalGrainKey, DateTime dateTimeTaken, string vitalType)
    {
        // Prevent duplicates
        if (_state.State.Entries.Any(e => e.VitalGrainKey == vitalGrainKey))
            return;

        VitalIndexEntry entry = new()
        {
            VitalGrainKey = vitalGrainKey,
            DateTimeTaken = dateTimeTaken,
            VitalType = vitalType
        };

        // Insert in sorted position (descending by date)
        int insertIndex = _state.State.Entries.FindIndex(e => e.DateTimeTaken <= dateTimeTaken);
        if (insertIndex < 0)
            _state.State.Entries.Add(entry);
        else
            _state.State.Entries.Insert(insertIndex, entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveVitalKeyAsync(string vitalGrainKey)
    {
        int removed = _state.State.Entries.RemoveAll(e => e.VitalGrainKey == vitalGrainKey);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<VitalIndexEntry>> GetAllKeysAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<VitalIndexEntry>> GetKeysByDateRangeAsync(DateTime from, DateTime to)
    {
        List<VitalIndexEntry> result = _state.State.Entries
            .Where(e => e.DateTimeTaken >= from && e.DateTimeTaken <= to)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<VitalIndexEntry>> GetKeysBeforeDateAsync(DateTime before, int maxCount)
    {
        List<VitalIndexEntry> result = _state.State.Entries
            .Where(e => e.DateTimeTaken < before)
            .Take(maxCount)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<VitalIndexEntry>> GetKeysByTypeAndDateRangeAsync(
        string vitalType, DateTime from, DateTime to)
    {
        List<VitalIndexEntry> result = _state.State.Entries
            .Where(e => e.VitalType == vitalType && e.DateTimeTaken >= from && e.DateTimeTaken <= to)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}
