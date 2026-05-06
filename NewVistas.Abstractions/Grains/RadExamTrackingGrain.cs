// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class RadExamTrackingGrain : Grain, IRadExamTrackingGrain
{
    private readonly IPersistentState<RadExamTrackingState> _state;
    public RadExamTrackingGrain([PersistentState("radExamTrackingState", "radExamTrackingStore")] IPersistentState<RadExamTrackingState> state) { _state = state; }

    public Task<RadExamTrackingState> GetAsync() => Task.FromResult(_state.State);

    public async Task InitializeAsync(string radiologyId, string patientId)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.RadiologyId = radiologyId;
        _state.State.PatientId = patientId;
        _state.State.ExamStatus = RadExamStatus.Ordered;
        _state.State.CreatedDate = now;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
    }

    public async Task ScheduleAsync(DateTime scheduledDateTime, string? room)
    {
        _state.State.ScheduledDateTime = scheduledDateTime;
        _state.State.Room = room;
        _state.State.ExamStatus = RadExamStatus.Scheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignProtocolAsync(string protocolId, string protocolName, string? protocolParameters)
    {
        _state.State.ProtocolId = protocolId;
        _state.State.ProtocolName = protocolName;
        _state.State.ProtocolParameters = protocolParameters;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkPatientPreppedAsync(string? prepNotes)
    {
        _state.State.ExamStatus = RadExamStatus.PatientPrepped;
        _state.State.PatientPreppedDateTime = DateTime.UtcNow;
        _state.State.PrepNotes = prepNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartExamAsync()
    {
        _state.State.ExamStatus = RadExamStatus.InProgress;
        _state.State.ExamStartDateTime = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteExamAsync(int? imageCount, string? techNotes)
    {
        _state.State.ExamStatus = RadExamStatus.ExamComplete;
        _state.State.ExamCompleteDateTime = DateTime.UtcNow;
        _state.State.ImageCount = imageCount;
        _state.State.TechNotes = techNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SendImagesToPacsAsync()
    {
        _state.State.ExamStatus = RadExamStatus.ImagesSentToPacs;
        _state.State.ImagesSentDateTime = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LinkImageAsync(string imageId)
    {
        if (!_state.State.LinkedImageIds.Contains(imageId))
            _state.State.LinkedImageIds.Add(imageId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelExamAsync()
    {
        _state.State.ExamStatus = RadExamStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
