// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class OncologyTreatmentGrain : Grain, IOncologyTreatmentGrain
{
    private readonly IPersistentState<OncologyTreatmentState> _state;

    public OncologyTreatmentGrain(
        [PersistentState("oncTreatmentState", "oncTreatmentStore")] IPersistentState<OncologyTreatmentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.TreatmentId))
        {
            _state.State.TreatmentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<OncologyTreatmentState> GetTreatmentAsync() => Task.FromResult(_state.State);

    public async Task CreateTreatmentAsync(
        string tumorId,
        string patientId,
        OncologyTreatmentType treatmentType,
        string agentName,
        string? doseDescription,
        string? providerId,
        string? providerName,
        string? facilityName,
        string? notes)
    {
        _state.State.TumorId = tumorId;
        _state.State.PatientId = patientId;
        _state.State.TreatmentType = treatmentType;
        _state.State.AgentName = agentName;
        _state.State.DoseDescription = doseDescription;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.FacilityName = facilityName;
        _state.State.Notes = notes;
        _state.State.Status = OncologyTreatmentStatus.Planned;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartTreatmentAsync(DateTime startDate)
    {
        _state.State.StartDate = startDate;
        _state.State.Status = OncologyTreatmentStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteTreatmentAsync(
        DateTime endDate,
        TreatmentResponseAssessment responseAssessment,
        string? notes)
    {
        _state.State.EndDate = endDate;
        _state.State.ResponseAssessment = responseAssessment;
        _state.State.ResponseAssessmentDate = endDate;
        _state.State.Status = OncologyTreatmentStatus.Completed;
        if (!string.IsNullOrWhiteSpace(notes))
            _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
                ? notes
                : $"{_state.State.Notes}\n{notes}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscontinueTreatmentAsync(
        DateTime endDate,
        string discontinuationReason,
        string? notes)
    {
        _state.State.EndDate = endDate;
        _state.State.DiscontinuationReason = discontinuationReason;
        _state.State.Status = OncologyTreatmentStatus.Discontinued;
        if (!string.IsNullOrWhiteSpace(notes))
            _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
                ? notes
                : $"{_state.State.Notes}\n{notes}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordResponseAsync(
        TreatmentResponseAssessment responseAssessment,
        DateTime assessmentDate,
        string? notes)
    {
        _state.State.ResponseAssessment = responseAssessment;
        _state.State.ResponseAssessmentDate = assessmentDate;
        if (!string.IsNullOrWhiteSpace(notes))
            _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
                ? notes
                : $"{_state.State.Notes}\n{notes}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateCyclesAsync(int cyclesCompleted)
    {
        _state.State.CyclesCompleted = cyclesCompleted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
