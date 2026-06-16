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
/// Index grain for all Home Telehealth readings belonging to a single patient.
/// Grain key: "HT-READING-IDX:{patientId}"
/// </summary>
public class HomeTelehealthReadingIndexGrain : Grain, IHomeTelehealthReadingIndexGrain
{
    private readonly IPersistentState<HomeTelehealthReadingIndexState> _state;

    public HomeTelehealthReadingIndexGrain(
        [PersistentState("htReadingIndexState", "htReadingIndexStore")] IPersistentState<HomeTelehealthReadingIndexState> state)
    {
        _state = state;
    }

    public async Task AddAsync(HtReadingIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<List<HtReadingIndexEntry>> GetAsync(HtMeasurementType? measurementType, int? days, int maxResults)
    {
        IEnumerable<HtReadingIndexEntry> query = _state.State.Entries;

        if (measurementType.HasValue)
            query = query.Where(e => e.MeasurementType == measurementType.Value);

        if (days.HasValue)
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(e => e.ReadingDateTime >= cutoff);
        }

        List<HtReadingIndexEntry> results = query
            .OrderByDescending(e => e.ReadingDateTime)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public async Task MarkReviewedAsync(string readingId)
    {
        HtReadingIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.ReadingId == readingId);
        if (entry != null)
        {
            entry.IsReviewed = true;
            await _state.WriteStateAsync();
        }
    }
}
