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
/// Per-patient Social Work Assessment index grain.
/// Key: "SW-ASSESSMENT-IDX:{patientId}"
/// </summary>
public class SocialWorkAssessmentIndexGrain : Grain, ISocialWorkAssessmentIndexGrain
{
    private readonly IPersistentState<SocialWorkAssessmentIndexState> _state;

    public SocialWorkAssessmentIndexGrain(
        [PersistentState("socialWorkAssessmentIndexState", "socialWorkAssessmentIndexStore")]
        IPersistentState<SocialWorkAssessmentIndexState> state)
    {
        _state = state;
    }

    public Task<List<SocialWorkAssessmentIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<SocialWorkAssessmentIndexEntry>> GetByTypeAsync(SocialWorkAssessmentType assessmentType)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.AssessmentType == assessmentType)
            .ToList());

    public Task<List<SocialWorkAssessmentIndexEntry>> GetByStatusAsync(SocialWorkAssessmentStatus status)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == status)
            .ToList());

    public async Task AddEntryAsync(SocialWorkAssessmentIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(string assessmentId, SocialWorkAssessmentStatus status)
    {
        SocialWorkAssessmentIndexEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.AssessmentId == assessmentId);
        if (entry != null)
        {
            entry.Status = status;
            await _state.WriteStateAsync();
        }
    }
}
