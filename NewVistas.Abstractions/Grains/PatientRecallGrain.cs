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
/// Optional feature grain for patient recall / automated recall letters.
/// Maps to IHS RPMS SC Recall (File #403.5).
/// Keyed by "SD-RECALL:{guid}".
/// </summary>
public class PatientRecallGrain : Grain, IPatientRecallGrain
{
    private readonly IPersistentState<PatientRecallState> _state;

    public PatientRecallGrain(
        [PersistentState("patientRecallState", "patientRecallStore")]
        IPersistentState<PatientRecallState> state)
    {
        _state = state;
    }

    public Task<PatientRecallState> GetEntryAsync() => Task.FromResult(_state.State);

    public async Task<PatientRecallState> CreateEntryAsync(
        string patientId, string patientName,
        string clinicId, string clinicName,
        string recallType, DateTime recallDate,
        string? providerId, string? providerName,
        string? diagnosis, string? instructions,
        string createdByProviderId, string createdByProviderName)
    {
        _state.State.EntryId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ClinicId = clinicId;
        _state.State.ClinicName = clinicName;
        _state.State.RecallType = recallType;
        _state.State.RecallDate = recallDate;
        _state.State.Status = "PENDING";
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Diagnosis = diagnosis;
        _state.State.Instructions = instructions;
        _state.State.CreatedByProviderId = createdByProviderId;
        _state.State.CreatedByProviderName = createdByProviderName;
        _state.State.LetterCount = 0;
        _state.State.ContactAttemptCount = 0;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();

        return _state.State;
    }

    public async Task UpdateRecallDateAsync(DateTime newRecallDate, string updatedByName)
    {
        _state.State.RecallDate = newRecallDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // If was overdue, reset to pending with new date
        if (_state.State.Status == "OVERDUE")
            _state.State.Status = "PENDING";

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task GenerateLetterAsync(string letterType, string generatedByName)
    {
        _state.State.Letters.Add(new RecallLetterEntry
        {
            GeneratedDate = DateTime.UtcNow,
            LetterType = letterType,
            GeneratedByName = generatedByName
        });
        _state.State.LetterCount++;

        if (_state.State.Status == "PENDING" || _state.State.Status == "OVERDUE")
            _state.State.Status = "LETTER_SENT";

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordContactAttemptAsync(
        string contactMethod, string result, string contactedByName, string? notes)
    {
        _state.State.ContactAttempts.Add(new RecallContactAttempt
        {
            AttemptDate = DateTime.UtcNow,
            ContactMethod = contactMethod,
            Result = result,
            ContactedByName = contactedByName,
            Notes = notes
        });
        _state.State.ContactAttemptCount++;

        // A successful contact only advances an entry still awaiting contact
        // (PENDING / LETTER_SENT / OVERDUE). Attempts against cancelled, completed,
        // or already-scheduled entries are recorded for the audit trail but never
        // change status — a REACHED call must not resurrect a CANCELLED recall.
        if (result == "REACHED" &&
            _state.State.Status is "PENDING" or "LETTER_SENT" or "OVERDUE")
        {
            _state.State.Status = "CONTACTED";
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task MarkAppointmentScheduledAsync(
        string appointmentId, DateTime appointmentDateTime, string scheduledByName)
    {
        _state.State.ScheduledAppointmentId = appointmentId;
        _state.State.ScheduledAppointmentDateTime = appointmentDateTime;
        _state.State.ScheduledByName = scheduledByName;
        _state.State.Status = "APPOINTMENT_SCHEDULED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task MarkCompletedAsync(string completedByName, string? notes)
    {
        _state.State.Status = "COMPLETED";
        _state.State.CompletionNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task CancelEntryAsync(string reason, string cancelledByName)
    {
        _state.State.CancellationReason = reason;
        _state.State.Status = "CANCELLED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task MarkOverdueAsync()
    {
        if (_state.State.Status is "PENDING" or "LETTER_SENT")
        {
            _state.State.Status = "OVERDUE";
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
            await UpdateIndexAsync();
        }
    }

    private async Task UpdateIndexAsync()
    {
        IPatientRecallIndexGrain index =
            GrainFactory.GetGrain<IPatientRecallIndexGrain>("SD-RECALL-IDX");

        await index.AddOrUpdateAsync(new PatientRecallIndexEntry
        {
            EntryId = _state.State.EntryId,
            PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName,
            ClinicId = _state.State.ClinicId,
            ClinicName = _state.State.ClinicName,
            RecallType = _state.State.RecallType,
            RecallDate = _state.State.RecallDate,
            Status = _state.State.Status,
            ProviderName = _state.State.ProviderName,
            LetterCount = _state.State.LetterCount,
            ContactAttemptCount = _state.State.ContactAttemptCount
        });
    }
}
