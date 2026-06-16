// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HHCVisitGrain : Grain, IHHCVisitGrain
{
    private readonly IPersistentState<HHCVisitState> _state;

    public HHCVisitGrain(
        [PersistentState("hhcVisitState", "hhcVisitStore")] IPersistentState<HHCVisitState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.VisitId))
            _state.State.VisitId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task ScheduleVisitAsync(
        string patientId,
        string patientName,
        DateTime visitDate,
        HHCVisitDiscipline discipline,
        HHCVisitType visitType,
        string clinicianId,
        string clinicianName,
        string notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.VisitDate = visitDate;
        _state.State.Discipline = discipline;
        _state.State.VisitType = visitType;
        _state.State.ClinicianId = clinicianId;
        _state.State.ClinicianName = clinicianName;
        _state.State.Notes = notes;
        _state.State.Status = HHCVisitStatus.Scheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteVisitAsync(
        int durationMinutes,
        string vitalSigns,
        List<string> interventions,
        string patientResponse,
        string goalsProgress,
        DateTime? nextVisitDate,
        string notes)
    {
        _state.State.Status = HHCVisitStatus.Completed;
        _state.State.DurationMinutes = durationMinutes;
        _state.State.VitalSigns = vitalSigns;
        _state.State.Interventions = interventions;
        _state.State.PatientResponse = patientResponse;
        _state.State.GoalsProgress = goalsProgress;
        _state.State.NextVisitDate = nextVisitDate;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelVisitAsync(string cancellationReason)
    {
        _state.State.Status = HHCVisitStatus.Cancelled;
        _state.State.CancellationReason = cancellationReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkNoAnswerAsync()
    {
        _state.State.Status = HHCVisitStatus.NoAnswer;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkPatientRefusedAsync(string notes)
    {
        _state.State.Status = HHCVisitStatus.PatientRefused;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HHCVisitState> GetVisitAsync() => Task.FromResult(_state.State);
}
