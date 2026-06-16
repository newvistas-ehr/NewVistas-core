// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class TBIScreeningGrain : Grain, ITBIScreeningGrain
{
    private readonly IPersistentState<TBIScreeningState> _state;

    public TBIScreeningGrain(
        [PersistentState("tbiScreeningState", "tbiScreeningStore")] IPersistentState<TBIScreeningState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ScreeningId))
            _state.State.ScreeningId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<TBIScreeningState> GetScreeningAsync() => Task.FromResult(_state.State);

    public async Task CreateScreeningAsync(
        string patientId,
        string patientName,
        DateTime screeningDate,
        string screeningLocation,
        string screenedById,
        string screenedByName,
        string encounterType,
        List<TBIScreeningAnswer> answers,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ScreeningDate = screeningDate;
        _state.State.ScreeningLocation = screeningLocation;
        _state.State.ScreenedById = screenedById;
        _state.State.ScreenedByName = screenedByName;
        _state.State.EncounterType = encounterType;
        _state.State.Answers = answers;
        _state.State.PositiveAnswerCount = answers.Count(a => a.Answer);
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FinalizeScreeningAsync(TBIScreeningResult result, bool triggeredFullEvaluation)
    {
        _state.State.Result = result;
        _state.State.TriggeredFullEvaluation = result == TBIScreeningResult.PositiveRequiresEvaluation && triggeredFullEvaluation;
        await _state.WriteStateAsync();
    }

    public async Task RecordFullEvaluationAsync(
        DateTime fullEvalDate,
        string providerId,
        string providerName,
        TBISeverity confirmedSeverity)
    {
        _state.State.FullEvaluationDate = fullEvalDate;
        _state.State.FullEvaluationProviderId = providerId;
        _state.State.FullEvaluationProviderName = providerName;
        _state.State.ConfirmedTBISeverity = confirmedSeverity;
        await _state.WriteStateAsync();
    }
}
