// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ResearchSubjectGrain : Grain, IResearchSubjectGrain
{
    private readonly IPersistentState<ResearchSubjectState> _state;

    public ResearchSubjectGrain(
        [PersistentState("irbSubjectState", "irbSubjectStore")] IPersistentState<ResearchSubjectState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SubjectId))
            _state.State.SubjectId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task EnrollSubjectAsync(
        string studyId,
        string studyTitle,
        string patientId,
        string patientName,
        DateTime? patientDOB,
        DateTime screeningDate,
        DateTime enrollmentDate,
        DateTime consentDate,
        ConsentType consentType,
        string consentObtainedBy,
        string arm)
    {
        _state.State.StudyId = studyId;
        _state.State.StudyTitle = studyTitle;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.PatientDOB = patientDOB;
        _state.State.ScreeningDate = screeningDate;
        _state.State.EnrollmentDate = enrollmentDate;
        _state.State.ConsentDate = consentDate;
        _state.State.ConsentType = consentType;
        _state.State.ConsentObtainedBy = consentObtainedBy;
        _state.State.Arm = arm;
        _state.State.EnrollmentStatus = SubjectEnrollmentStatus.Enrolled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ActivateSubjectAsync()
    {
        _state.State.EnrollmentStatus = SubjectEnrollmentStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task WithdrawSubjectAsync(string reason, DateTime withdrawalDate)
    {
        _state.State.EnrollmentStatus = SubjectEnrollmentStatus.Withdrawn;
        _state.State.WithdrawalReason = reason;
        _state.State.WithdrawalDate = withdrawalDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteSubjectAsync(DateTime completionDate)
    {
        _state.State.EnrollmentStatus = SubjectEnrollmentStatus.Completed;
        _state.State.CompletionDate = completionDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkLostToFollowUpAsync()
    {
        _state.State.EnrollmentStatus = SubjectEnrollmentStatus.LostToFollowUp;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeceasedAsync(string notes)
    {
        _state.State.EnrollmentStatus = SubjectEnrollmentStatus.Deceased;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateArmAsync(string arm)
    {
        _state.State.Arm = arm;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddNotesAsync(string notes)
    {
        _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
            ? notes
            : $"{_state.State.Notes}\n{notes}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<ResearchSubjectState> GetSubjectAsync() => Task.FromResult(_state.State);
}
