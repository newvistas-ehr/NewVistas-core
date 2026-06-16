// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class CPExamGrain : Grain, ICPExamGrain
{
    private readonly IPersistentState<CPExamState> _state;

    public CPExamGrain(
        [PersistentState("cpExamState", "cpExamStore")] IPersistentState<CPExamState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ExamId))
        {
            _state.State.ExamId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task ScheduleExamAsync(
        string patientId,
        string patientName,
        CPExamType examType,
        DateTime scheduledDate,
        string examinerName,
        string examinerTitle,
        CPExaminerType examinerType,
        string examLocation,
        string examFacility,
        string claimNumber,
        string benefitType,
        List<string> disabilityClaimedCodes,
        string createdBy)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ExamType = examType;
        _state.State.ScheduledDate = scheduledDate;
        _state.State.ExaminerName = examinerName;
        _state.State.ExaminerTitle = examinerTitle;
        _state.State.ExaminerType = examinerType;
        _state.State.ExamLocation = examLocation;
        _state.State.ExamFacility = examFacility;
        _state.State.ClaimNumber = claimNumber;
        _state.State.BenefitType = benefitType;
        _state.State.DisabilityClaimedCodes = disabilityClaimedCodes;
        _state.State.CreatedBy = createdBy;
        _state.State.Status = CPExamStatus.Scheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteExamAsync(List<string> diagnoses, bool nexus, string nexusRationale)
    {
        _state.State.Diagnoses = diagnoses;
        _state.State.Nexus = nexus;
        _state.State.NexusRationale = nexusRationale;
        _state.State.Status = CPExamStatus.Completed;
        _state.State.CompletedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelExamAsync(string cancellationReason)
    {
        _state.State.CancellationReason = cancellationReason;
        _state.State.Status = CPExamStatus.Cancelled;
        _state.State.CancelledDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RescheduleExamAsync(DateTime newScheduledDate, string reason)
    {
        _state.State.ScheduledDate = newScheduledDate;
        _state.State.CancellationReason = reason;
        _state.State.Status = CPExamStatus.Rescheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddDbqToExamAsync(string dbqId)
    {
        if (!_state.State.DbqIds.Contains(dbqId))
        {
            _state.State.DbqIds.Add(dbqId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<CPExamState> GetExamAsync() => Task.FromResult(_state.State);
}
