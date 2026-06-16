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
/// Optional feature grain for appointment wait list entries with auto-rebooking.
/// Maps to IHS RPMS SD Wait List (File #409.3).
/// Keyed by "SD-WL:{guid}".
/// </summary>
public class AppointmentWaitListGrain : Grain, IAppointmentWaitListGrain
{
    private readonly IPersistentState<AppointmentWaitListState> _state;

    public AppointmentWaitListGrain(
        [PersistentState("appointmentWaitListState", "appointmentWaitListStore")]
        IPersistentState<AppointmentWaitListState> state)
    {
        _state = state;
    }

    public Task<AppointmentWaitListState> GetEntryAsync() => Task.FromResult(_state.State);

    public async Task<AppointmentWaitListState> CreateEntryAsync(
        string patientId, string patientName,
        string clinicId, string clinicName,
        string desiredAppointmentType,
        string? preferredProviderId, string? preferredProviderName,
        string priority,
        DateTime? desiredDateRangeStart, DateTime? desiredDateRangeEnd,
        string? comments,
        string createdByProviderId, string createdByProviderName)
    {
        _state.State.EntryId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ClinicId = clinicId;
        _state.State.ClinicName = clinicName;
        _state.State.DesiredAppointmentType = desiredAppointmentType;
        _state.State.PreferredProviderId = preferredProviderId;
        _state.State.PreferredProviderName = preferredProviderName;
        _state.State.Priority = priority;
        _state.State.Status = "WAITING";
        _state.State.DesiredDateRangeStart = desiredDateRangeStart;
        _state.State.DesiredDateRangeEnd = desiredDateRangeEnd;
        _state.State.Comments = comments;
        _state.State.CreatedByProviderId = createdByProviderId;
        _state.State.CreatedByProviderName = createdByProviderName;
        _state.State.WaitListDate = DateTime.UtcNow;
        _state.State.OfferCount = 0;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "CREATED",
            PerformedByName = createdByProviderName,
            Details = $"Added to wait list for {clinicName}, priority {priority}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        return _state.State;
    }

    public async Task UpdatePriorityAsync(string priority)
    {
        string oldPriority = _state.State.Priority;
        _state.State.Priority = priority;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "PRIORITY_CHANGED",
            PerformedByName = "SYSTEM",
            Details = $"Priority changed from {oldPriority} to {priority}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task UpdateDesiredDateRangeAsync(DateTime? rangeStart, DateTime? rangeEnd)
    {
        _state.State.DesiredDateRangeStart = rangeStart;
        _state.State.DesiredDateRangeEnd = rangeEnd;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "DATE_RANGE_UPDATED",
            PerformedByName = "SYSTEM",
            Details = $"Date range updated: {rangeStart:d} – {rangeEnd:d}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task OfferSlotAsync(string appointmentId, DateTime offeredDateTime, string offeredByName)
    {
        _state.State.OfferedAppointmentId = appointmentId;
        _state.State.OfferedDateTime = offeredDateTime;
        _state.State.OfferDate = DateTime.UtcNow;
        _state.State.OfferedByName = offeredByName;
        _state.State.Status = "OFFERED";
        _state.State.OfferCount++;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "SLOT_OFFERED",
            PerformedByName = offeredByName,
            Details = $"Offered slot at {offeredDateTime:g} (offer #{_state.State.OfferCount})"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task AcceptOfferAsync(string acceptedByName)
    {
        if (_state.State.Status != "OFFERED")
            throw new InvalidOperationException("No pending offer to accept.");

        _state.State.BookedAppointmentId = _state.State.OfferedAppointmentId;
        _state.State.BookedDateTime = _state.State.OfferedDateTime;
        _state.State.BookedDate = DateTime.UtcNow;
        _state.State.BookedByName = acceptedByName;
        _state.State.Status = "BOOKED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "OFFER_ACCEPTED",
            PerformedByName = acceptedByName,
            Details = $"Accepted offered slot at {_state.State.BookedDateTime:g}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task DeclineOfferAsync(string reason, string declinedByName)
    {
        if (_state.State.Status != "OFFERED")
            throw new InvalidOperationException("No pending offer to decline.");

        _state.State.DeclineReason = reason;
        _state.State.OfferedAppointmentId = null;
        _state.State.OfferedDateTime = null;
        _state.State.OfferDate = null;
        _state.State.OfferedByName = null;
        _state.State.Status = "WAITING";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "OFFER_DECLINED",
            PerformedByName = declinedByName,
            Details = $"Declined offer: {reason}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task BookFromWaitListAsync(string appointmentId, DateTime bookedDateTime, string bookedByName)
    {
        _state.State.BookedAppointmentId = appointmentId;
        _state.State.BookedDateTime = bookedDateTime;
        _state.State.BookedDate = DateTime.UtcNow;
        _state.State.BookedByName = bookedByName;
        _state.State.Status = "BOOKED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "BOOKED_FROM_WAITLIST",
            PerformedByName = bookedByName,
            Details = $"Booked appointment {appointmentId} at {bookedDateTime:g}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task CancelEntryAsync(string reason, string cancelledByName)
    {
        _state.State.CancellationReason = reason;
        _state.State.Status = "CANCELLED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "CANCELLED",
            PerformedByName = cancelledByName,
            Details = $"Cancelled: {reason}"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task ExpireEntryAsync()
    {
        _state.State.Status = "EXPIRED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.AuditTrail.Add(new WaitListAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = "EXPIRED",
            PerformedByName = "SYSTEM",
            Details = "Wait list entry expired (past desired date range)"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    private async Task UpdateIndexAsync()
    {
        IAppointmentWaitListIndexGrain index =
            GrainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");

        await index.AddOrUpdateAsync(new AppointmentWaitListIndexEntry
        {
            EntryId = _state.State.EntryId,
            PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName,
            ClinicId = _state.State.ClinicId,
            ClinicName = _state.State.ClinicName,
            DesiredAppointmentType = _state.State.DesiredAppointmentType,
            Priority = _state.State.Priority,
            Status = _state.State.Status,
            WaitListDate = _state.State.WaitListDate,
            DesiredDateRangeStart = _state.State.DesiredDateRangeStart,
            DesiredDateRangeEnd = _state.State.DesiredDateRangeEnd,
            PreferredProviderName = _state.State.PreferredProviderName,
            OfferCount = _state.State.OfferCount
        });
    }
}
