// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeVisitGrain : Grain, IHomeVisitGrain
{
    private readonly IPersistentState<HomeVisitState> _state;

    public HomeVisitGrain(
        [PersistentState("homeVisitState", "homeVisitStore")] IPersistentState<HomeVisitState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.VisitId))
        {
            _state.State.VisitId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task ScheduleAsync(
        string episodeId,
        string patientId,
        string patientName,
        HomeCareDiscipline discipline,
        HomeVisitType visitType,
        DateTime scheduledDateTime,
        string clinicianId,
        string clinicianName,
        string reason)
    {
        _state.State.EpisodeId = episodeId;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.Discipline = discipline;
        _state.State.VisitType = visitType;
        _state.State.ScheduledDateTime = scheduledDateTime;
        _state.State.ClinicianId = clinicianId;
        _state.State.ClinicianName = clinicianName;
        _state.State.Reason = reason;
        _state.State.Status = HomeVisitStatus.Scheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartAsync()
    {
        _state.State.Status = HomeVisitStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(
        int durationMinutes,
        string vitalSigns,
        List<string> interventions,
        string summary,
        string noteId,
        DateTime? nextVisitDate)
    {
        _state.State.Status = HomeVisitStatus.Completed;
        _state.State.DurationMinutes = durationMinutes;
        _state.State.VitalSigns = vitalSigns;
        _state.State.Interventions = interventions ?? new();
        _state.State.Summary = summary;
        _state.State.NoteId = noteId;
        _state.State.NextVisitDate = nextVisitDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(HomeVisitStatus status, string reason)
    {
        _state.State.Status = status;
        _state.State.CancellationReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CheckInAsync(DateTime time, string location, EvvMethod method)
    {
        _state.State.CheckInTime = time;
        _state.State.CheckInLocation = location;
        _state.State.EvvMethod = method;
        _state.State.Status = HomeVisitStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CheckOutAsync(DateTime time, string location)
    {
        _state.State.CheckOutTime = time;
        _state.State.CheckOutLocation = location;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HomeVisitState> GetVisitAsync() => Task.FromResult(_state.State);
}
