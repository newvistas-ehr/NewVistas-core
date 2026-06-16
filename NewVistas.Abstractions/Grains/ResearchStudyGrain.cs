// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ResearchStudyGrain : Grain, IResearchStudyGrain
{
    private readonly IPersistentState<ResearchStudyState> _state;

    public ResearchStudyGrain(
        [PersistentState("irbStudyState", "irbStudyStore")] IPersistentState<ResearchStudyState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.StudyId))
            _state.State.StudyId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateStudyAsync(
        string irbProtocolNumber,
        string title,
        string shortTitle,
        string principalInvestigator,
        string piEmployeeId,
        string sponsor,
        IrbStudyType studyType,
        IrbStudyPhase phase,
        string department,
        int targetEnrollment,
        string description)
    {
        _state.State.IrbProtocolNumber = irbProtocolNumber;
        _state.State.Title = title;
        _state.State.ShortTitle = shortTitle;
        _state.State.PrincipalInvestigator = principalInvestigator;
        _state.State.PiEmployeeId = piEmployeeId;
        _state.State.Sponsor = sponsor;
        _state.State.StudyType = studyType;
        _state.State.Phase = phase;
        _state.State.Department = department;
        _state.State.TargetEnrollment = targetEnrollment;
        _state.State.Description = description;
        _state.State.Status = IrbStudyStatus.Draft;
        _state.State.CurrentEnrollment = 0;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task OpenForEnrollmentAsync(
        DateTime approvalDate,
        DateTime expirationDate,
        DateTime? nextContinuingReviewDue)
    {
        _state.State.Status = IrbStudyStatus.OpenForEnrollment;
        _state.State.InitialApprovalDate ??= approvalDate;
        _state.State.CurrentExpirationDate = expirationDate;
        _state.State.NextContinuingReviewDue = nextContinuingReviewDue;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseToEnrollmentAsync()
    {
        _state.State.Status = IrbStudyStatus.ClosedToEnrollment;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SuspendStudyAsync(string reason)
    {
        _state.State.Status = IrbStudyStatus.Suspended;
        _state.State.Description = string.IsNullOrEmpty(reason)
            ? _state.State.Description
            : $"{_state.State.Description}\n[SUSPENDED: {reason}]";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteStudyAsync()
    {
        _state.State.Status = IrbStudyStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task WithdrawStudyAsync(string reason)
    {
        _state.State.Status = IrbStudyStatus.Withdrawn;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddArmAsync(string armName)
    {
        if (!_state.State.StudyArms.Contains(armName))
            _state.State.StudyArms.Add(armName);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateTargetEnrollmentAsync(int targetEnrollment)
    {
        _state.State.TargetEnrollment = targetEnrollment;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordSubmissionAsync(
        string submissionId,
        IrbSubmissionType submissionType,
        DateTime submissionDate,
        string notes)
    {
        _state.State.Submissions.Add(new IrbSubmissionEntry
        {
            SubmissionId = submissionId,
            SubmissionType = submissionType,
            SubmissionDate = submissionDate,
            Status = IrbSubmissionStatus.Submitted,
            Notes = notes
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateSubmissionDecisionAsync(
        string submissionId,
        IrbSubmissionStatus status,
        string decision,
        DateTime reviewDate,
        DateTime? newExpirationDate)
    {
        IrbSubmissionEntry? submission = _state.State.Submissions
            .FirstOrDefault(s => s.SubmissionId == submissionId);
        if (submission is not null)
        {
            submission.Status = status;
            submission.Decision = decision;
            submission.ReviewDate = reviewDate;
            if (newExpirationDate.HasValue)
            {
                submission.NewExpirationDate = newExpirationDate;
                _state.State.CurrentExpirationDate = newExpirationDate;
            }
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task IncrementEnrollmentAsync()
    {
        _state.State.CurrentEnrollment++;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DecrementEnrollmentAsync()
    {
        if (_state.State.CurrentEnrollment > 0)
            _state.State.CurrentEnrollment--;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<ResearchStudyState> GetStudyAsync() => Task.FromResult(_state.State);
}
