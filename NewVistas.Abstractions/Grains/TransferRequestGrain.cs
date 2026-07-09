// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Transfers;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Inter-facility transfer request state machine (ConsultGrain pattern: idempotent
/// create, status strings, actor-stamped timeline, clinical-event outbox).
/// </summary>
public class TransferRequestGrain : Grain, ITransferRequestGrain
{
    private readonly IPersistentState<TransferRequestState> _state;

    public TransferRequestGrain(
        [PersistentState("transferRequest", "transferRequestStore")]
        IPersistentState<TransferRequestState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.TransferId))
            _state.State.TransferId = this.GetPrimaryKeyString();
        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private string? CurrentUserId => RequestContext.Get(RequestContextKeys.UserId) as string;
    private string? CurrentUserName => RequestContext.Get(RequestContextKeys.UserName) as string;

    public Task<TransferRequestState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId, string? patientName,
        string sendingInstitutionId, string? sendingInstitutionName,
        string? sendingUnitId, string? sendingAdmissionId,
        string? sendingAttendingId, string? sendingAttendingName,
        string receivingInstitutionId, string? receivingInstitutionName,
        string? requestedLevelOfCare, BedType? requestedBedType, BedIsolationType isolationRequired,
        string urgency, string? clinicalSummary, string? reasonForTransfer)
    {
        // Idempotent: re-issued request on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.SendingInstitutionId = sendingInstitutionId;
        _state.State.SendingInstitutionName = sendingInstitutionName;
        _state.State.SendingUnitId = sendingUnitId;
        _state.State.SendingAdmissionId = sendingAdmissionId;
        _state.State.SendingAttendingId = sendingAttendingId;
        _state.State.SendingAttendingName = sendingAttendingName;
        _state.State.ReceivingInstitutionId = receivingInstitutionId;
        _state.State.ReceivingInstitutionName = receivingInstitutionName;
        _state.State.RequestedLevelOfCare = requestedLevelOfCare;
        _state.State.RequestedBedType = requestedBedType;
        _state.State.IsolationRequired = isolationRequired;
        _state.State.Urgency = urgency;
        _state.State.ClinicalSummary = clinicalSummary;
        _state.State.ReasonForTransfer = reasonForTransfer;
        _state.State.Status = TransferRequestStatus.Requested;
        _state.State.RequestDateTime = DateTime.UtcNow;
        AppendTimeline(TransferRequestStatus.Requested, reasonForTransfer);

        var evt = new TransferRequestedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            TransferId = _state.State.TransferId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await SaveAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task AcceptAsync(string unitId, string bedId, string? note)
    {
        // Idempotent when already accepted with the same bed.
        if (_state.State.Status == TransferRequestStatus.Accepted
            && _state.State.ReservedUnitId == unitId && _state.State.ReservedBedId == bedId)
            return;

        RequireStatus(TransferRequestStatus.Requested, "accept");

        _state.State.Status = TransferRequestStatus.Accepted;
        _state.State.ReservedUnitId = unitId;
        _state.State.ReservedBedId = bedId;
        _state.State.AcceptedDateTime = DateTime.UtcNow;
        AppendTimeline(TransferRequestStatus.Accepted, note ?? $"Bed {bedId} on {unitId} reserved.");

        var evt = new TransferAcceptedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            TransferId = _state.State.TransferId,
            ReceivingInstitutionId = _state.State.ReceivingInstitutionId,
            ReservedUnitId = unitId,
            ReservedBedId = bedId
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await SaveAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task ReassignBedAsync(string unitId, string bedId, string? note)
    {
        RequireStatus(TransferRequestStatus.Accepted, "reassign the bed for");

        _state.State.ReservedUnitId = unitId;
        _state.State.ReservedBedId = bedId;
        AppendTimeline(TransferRequestStatus.Accepted, note ?? $"Reservation moved to bed {bedId} on {unitId}.");
        await SaveAsync();
    }

    public async Task CompleteAsync(DateTime arrivalDateTime, string dischargeAdtId, string admissionAdtId,
        string? receivingAttendingId, string? receivingAttendingName)
    {
        if (_state.State.Status == TransferRequestStatus.Completed)
            return; // idempotent

        RequireStatus(TransferRequestStatus.Accepted, "complete");

        _state.State.Status = TransferRequestStatus.Completed;
        _state.State.DischargeAdtId = dischargeAdtId;
        _state.State.AdmissionAdtId = admissionAdtId;
        _state.State.ReceivingAttendingId = receivingAttendingId;
        _state.State.ReceivingAttendingName = receivingAttendingName;
        _state.State.CompletedDateTime = arrivalDateTime;
        AppendTimeline(TransferRequestStatus.Completed, "Patient arrived and admitted.");

        var evt = new TransferCompletedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            TransferId = _state.State.TransferId,
            DischargeAdtId = dischargeAdtId,
            AdmissionAdtId = admissionAdtId,
            ArrivalDateTime = arrivalDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await SaveAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task DeclineAsync(string reason)
    {
        if (_state.State.Status == TransferRequestStatus.Declined)
            return; // idempotent

        RequireStatus(TransferRequestStatus.Requested, "decline");

        _state.State.Status = TransferRequestStatus.Declined;
        _state.State.DeclineReason = reason;
        AppendTimeline(TransferRequestStatus.Declined, reason);

        var evt = new TransferDeclinedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            TransferId = _state.State.TransferId,
            Reason = reason
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await SaveAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CancelAsync(string? reason)
    {
        if (_state.State.Status == TransferRequestStatus.Cancelled)
            return; // idempotent

        if (_state.State.Status is not (TransferRequestStatus.Requested or TransferRequestStatus.Accepted))
            throw new InvalidOperationException(
                $"Transfer {_state.State.TransferId} is {_state.State.Status} — it cannot be cancelled.");

        _state.State.Status = TransferRequestStatus.Cancelled;
        _state.State.CancelReason = reason;
        AppendTimeline(TransferRequestStatus.Cancelled, reason);

        var evt = new TransferCancelledV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            TransferId = _state.State.TransferId,
            Reason = reason
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await SaveAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    // ─── Internals ───────────────────────────────────────────────────────

    private void RequireStatus(string expected, string verb)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            throw new InvalidOperationException($"Transfer {_state.State.TransferId} does not exist.");
        if (_state.State.Status != expected)
            throw new InvalidOperationException(
                $"Transfer {_state.State.TransferId} is {_state.State.Status} — cannot {verb} it (requires {expected}).");
    }

    private void AppendTimeline(string status, string? note)
        => _state.State.Timeline.Add(new TransferStatusEvent
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            ActorId = CurrentUserId,
            ActorName = CurrentUserName,
            Note = note
        });

    private async Task SaveAsync()
    {
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
