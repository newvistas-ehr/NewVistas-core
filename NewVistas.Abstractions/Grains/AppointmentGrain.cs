// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Scheduling;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Appointment Grain implementation based on VistA SDEC APPOINTMENT file (#409.84)
/// </summary>
public class AppointmentGrain : Grain, IAppointmentGrain
{
    private readonly IPersistentState<AppointmentState> _state;

    public AppointmentGrain(
        [PersistentState("appointmentState", "appointmentStore")] IPersistentState<AppointmentState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AppointmentId))
        {
            _state.State.AppointmentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private string? CurrentUserId => RequestContext.Get(RequestContextKeys.UserId) as string;
    private string? CurrentUserName => RequestContext.Get(RequestContextKeys.UserName) as string;

    public Task<AppointmentState> GetAppointmentAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task ScheduleAppointmentAsync(
        string patientId,
        string clinicId,
        string clinicName,
        DateTime appointmentDateTime,
        int durationMinutes,
        string? providerId,
        string? providerName,
        string? purpose,
        string? appointmentType,
        string? createdBy,
        bool isDoubleBook = false)
    {
        // Idempotent: re-issued schedule on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.ClinicId = clinicId;
        _state.State.ClinicName = clinicName;
        _state.State.AppointmentDateTime = appointmentDateTime;
        _state.State.DurationMinutes = durationMinutes;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Purpose = purpose;
        _state.State.AppointmentType = appointmentType;
        _state.State.IsDoubleBook = isDoubleBook;
        _state.State.Status = "Scheduled";
        _state.State.CreatedBy = createdBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new AppointmentScheduledV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            AppointmentId = _state.State.AppointmentId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task UpdateAppointmentAsync(
        DateTime? appointmentDateTime,
        int? durationMinutes,
        string? providerId,
        string? providerName,
        string? purpose,
        string? notes,
        string? modifiedBy)
    {
        if (appointmentDateTime.HasValue)
            _state.State.AppointmentDateTime = appointmentDateTime.Value;

        if (durationMinutes.HasValue)
            _state.State.DurationMinutes = durationMinutes.Value;

        if (providerId != null)
            _state.State.ProviderId = providerId;

        if (providerName != null)
            _state.State.ProviderName = providerName;

        if (purpose != null)
            _state.State.Purpose = purpose;

        if (notes != null)
            _state.State.Notes = notes;

        _state.State.ModifiedBy = modifiedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task CheckInAsync(DateTime checkInDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.CheckInDateTime.HasValue) return; // already checked in

        _state.State.CheckInDateTime = checkInDateTime;
        _state.State.Status = "Checked In";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new AppointmentCheckedInV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            AppointmentId = _state.State.AppointmentId,
            CheckInDateTime = checkInDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CheckOutAsync(DateTime checkOutDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.CheckOutDateTime.HasValue) return; // already checked out

        _state.State.CheckOutDateTime = checkOutDateTime;
        _state.State.Status = "Checked Out";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new AppointmentCheckedOutV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            AppointmentId = _state.State.AppointmentId,
            CheckOutDateTime = checkOutDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CancelAppointmentAsync(string cancellationReason, string? cancelledBy)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.Status == "Cancelled") return;

        DateTime now = DateTime.UtcNow;
        _state.State.Status = "Cancelled";
        _state.State.CancellationReason = cancellationReason;
        _state.State.CancellationDateTime = now;
        _state.State.ModifiedBy = cancelledBy;
        _state.State.LastModifiedDate = now;

        var evt = new AppointmentCancelledV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = now,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            AppointmentId = _state.State.AppointmentId,
            CancellationReason = cancellationReason,
            CancellationDateTime = now
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task MarkAsNoShowAsync()
    {
        _state.State.IsNoShow = true;
        _state.State.Status = "No-Show";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task CompleteAppointmentAsync()
    {
        _state.State.Status = "Completed";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateNotesAsync(string notes)
    {
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkReminderSentAsync()
    {
        _state.State.ReminderSent = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<DateTime> GetAppointmentDateTimeAsync()
    {
        return Task.FromResult(_state.State.AppointmentDateTime);
    }

    public Task<string> GetPatientIdAsync()
    {
        return Task.FromResult(_state.State.PatientId);
    }

    public Task<bool> IsPastAppointmentAsync()
    {
        return Task.FromResult(_state.State.AppointmentDateTime < DateTime.UtcNow);
    }
}
