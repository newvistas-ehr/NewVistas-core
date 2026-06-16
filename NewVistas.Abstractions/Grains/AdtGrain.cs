// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Adt;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// ADT (Admission/Discharge/Transfer) Grain implementation based on VistA PATIENT MOVEMENT file (#405)
/// </summary>
public class AdtGrain : Grain, IAdtGrain
{
    private readonly IPersistentState<AdtState> _state;

    public AdtGrain(
        [PersistentState("adtState", "adtStore")] IPersistentState<AdtState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.MovementId))
        {
            _state.State.MovementId = this.GetPrimaryKeyString();
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

    public Task<AdtState> GetMovementAsync() => Task.FromResult(_state.State);

    public async Task RecordAdmissionAsync(
        string patientId, DateTime movementDateTime,
        string? wardLocationId, string? wardLocationName,
        string? roomBed, string? treatingSpecialtyId,
        string? treatingSpecialtyName,
        string? attendingPhysicianId, string? attendingPhysicianName,
        string? typeOfPatient, string? admissionDiagnosis, string? comments)
    {
        // Idempotent: re-issued admission on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.TransactionType = "ADMISSION";
        _state.State.MovementDateTime = movementDateTime;
        _state.State.AdmissionDateTime = movementDateTime;
        _state.State.WardLocationId = wardLocationId;
        _state.State.WardLocationName = wardLocationName;
        _state.State.RoomBed = roomBed;
        _state.State.TreatingSpecialtyId = treatingSpecialtyId;
        _state.State.TreatingSpecialtyName = treatingSpecialtyName;
        _state.State.AttendingPhysicianId = attendingPhysicianId;
        _state.State.AttendingPhysicianName = attendingPhysicianName;
        _state.State.TypeOfPatient = typeOfPatient;
        _state.State.AdmissionDiagnosis = admissionDiagnosis;
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new AdmissionRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            MovementId = _state.State.MovementId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task RecordTransferAsync(
        string? wardLocationId, string? wardLocationName,
        string? roomBed, string? treatingSpecialtyId,
        string? treatingSpecialtyName, DateTime movementDateTime)
    {
        _state.State.TransactionType = "TRANSFER";
        _state.State.MovementDateTime = movementDateTime;
        _state.State.WardLocationId = wardLocationId;
        _state.State.WardLocationName = wardLocationName;
        _state.State.RoomBed = roomBed;
        _state.State.TreatingSpecialtyId = treatingSpecialtyId;
        _state.State.TreatingSpecialtyName = treatingSpecialtyName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAsTransferAsync(
        string patientId, DateTime admissionDateTime, DateTime transferDateTime,
        string? toWardLocationId, string? toWardLocationName, string? toRoomBed,
        string? toTreatingSpecialtyId, string? toTreatingSpecialtyName,
        string? attendingPhysicianId, string? attendingPhysicianName, string? comments)
    {
        // Idempotent: re-issued transfer on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.TransactionType = "TRANSFER";
        _state.State.AdmissionDateTime = admissionDateTime;
        _state.State.MovementDateTime = transferDateTime;
        _state.State.WardLocationId = toWardLocationId;
        _state.State.WardLocationName = toWardLocationName;
        _state.State.RoomBed = toRoomBed;
        _state.State.TreatingSpecialtyId = toTreatingSpecialtyId;
        _state.State.TreatingSpecialtyName = toTreatingSpecialtyName;
        _state.State.AttendingPhysicianId = attendingPhysicianId;
        _state.State.AttendingPhysicianName = attendingPhysicianName;
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new TransferRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            MovementId = _state.State.MovementId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task RecordDischargeAsync(
        DateTime dischargeDateTime, string? dischargeDiagnosis,
        string? disposition, string? comments)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.DischargeDateTime.HasValue) return; // already discharged

        _state.State.TransactionType = "DISCHARGE";
        _state.State.MovementDateTime = dischargeDateTime;
        _state.State.DischargeDateTime = dischargeDateTime;
        _state.State.DischargeDiagnosis = dischargeDiagnosis;
        _state.State.Disposition = disposition;
        _state.State.Comments = comments;
        if (_state.State.AdmissionDateTime.HasValue)
            _state.State.LengthOfStay = (int)(dischargeDateTime - _state.State.AdmissionDateTime.Value).TotalDays;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new DischargeRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            MovementId = _state.State.MovementId,
            DischargeDateTime = dischargeDateTime,
            DischargeDiagnosis = dischargeDiagnosis,
            Disposition = disposition,
            LengthOfStay = _state.State.LengthOfStay
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }
}
