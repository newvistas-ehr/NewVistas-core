// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Individual research subject (enrolled patient) grain.
/// Key pattern: "IRB-SUBJECT:{guid}".
/// </summary>
public interface IResearchSubjectGrain : IGrainWithStringKey
{
    Task EnrollSubjectAsync(
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
        string arm);

    Task ActivateSubjectAsync();
    Task WithdrawSubjectAsync(string reason, DateTime withdrawalDate);
    Task CompleteSubjectAsync(DateTime completionDate);
    Task MarkLostToFollowUpAsync();
    Task MarkDeceasedAsync(string notes);
    Task UpdateArmAsync(string arm);
    Task AddNotesAsync(string notes);

    Task<ResearchSubjectState> GetSubjectAsync();
}
