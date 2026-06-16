// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Individual research study / IRB protocol grain.
/// Key pattern: "IRB-STUDY:{guid}".
/// VistA Research Module (~File #900). RCRJ.m, RCRTX.m
/// </summary>
public interface IResearchStudyGrain : IGrainWithStringKey
{
    Task CreateStudyAsync(
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
        string description);

    Task OpenForEnrollmentAsync(
        DateTime approvalDate,
        DateTime expirationDate,
        DateTime? nextContinuingReviewDue);

    Task CloseToEnrollmentAsync();
    Task SuspendStudyAsync(string reason);
    Task CompleteStudyAsync();
    Task WithdrawStudyAsync(string reason);
    Task AddArmAsync(string armName);
    Task UpdateTargetEnrollmentAsync(int targetEnrollment);

    Task RecordSubmissionAsync(
        string submissionId,
        IrbSubmissionType submissionType,
        DateTime submissionDate,
        string notes);

    Task UpdateSubmissionDecisionAsync(
        string submissionId,
        IrbSubmissionStatus status,
        string decision,
        DateTime reviewDate,
        DateTime? newExpirationDate);

    Task IncrementEnrollmentAsync();
    Task DecrementEnrollmentAsync();

    Task<ResearchStudyState> GetStudyAsync();
}
