// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeCareAssessmentGrain : Grain, IHomeCareAssessmentGrain
{
    private readonly IPersistentState<HomeCareAssessmentState> _state;

    public HomeCareAssessmentGrain(
        [PersistentState("homeCareAssessmentState", "homeCareAssessmentStore")] IPersistentState<HomeCareAssessmentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AssessmentId))
        {
            _state.State.AssessmentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordComprehensiveAsync(
        string episodeId,
        string patientId,
        string assessorId,
        string assessorName,
        DateTime assessmentDate,
        HbpcComprehensiveAssessment assessment)
    {
        _state.State.EpisodeId = episodeId;
        _state.State.PatientId = patientId;
        _state.State.AssessmentType = HomeCareAssessmentType.ComprehensiveHbpc;
        _state.State.AssessorId = assessorId;
        _state.State.AssessorName = assessorName;
        _state.State.AssessmentDate = assessmentDate;
        _state.State.Comprehensive = assessment ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordOasisAsync(
        string episodeId,
        string patientId,
        HomeCareAssessmentType assessmentType,
        string assessorId,
        string assessorName,
        DateTime assessmentDate,
        OasisDataSet oasis)
    {
        _state.State.EpisodeId = episodeId;
        _state.State.PatientId = patientId;
        _state.State.AssessmentType = assessmentType;
        _state.State.AssessorId = assessorId;
        _state.State.AssessorName = assessorName;
        _state.State.AssessmentDate = assessmentDate;
        _state.State.Oasis = oasis;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HomeCareAssessmentState> GetAssessmentAsync() => Task.FromResult(_state.State);
}
