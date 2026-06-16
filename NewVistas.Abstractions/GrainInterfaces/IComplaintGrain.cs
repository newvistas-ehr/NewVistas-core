// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single patient complaint or grievance.
/// VistA File #745 (PATIENT REPRESENTATIVE). PATREPE.m
/// </summary>
public interface IComplaintGrain : IGrainWithStringKey
{
    Task FileComplaintAsync(
        string patientId,
        string patientName,
        DateTime complaintDate,
        ComplaintType complaintType,
        ComplaintCategory category,
        ComplaintPriority priority,
        InquirySource source,
        string narrativeDescription,
        string specificConcern,
        string departmentInvolved,
        string reporterName,
        string reporterRelationship,
        bool isConfidential,
        string createdBy);

    Task AssignAdvocateAsync(string advocateId, string advocateName);
    Task AcknowledgeComplaintAsync(DateTime acknowledgmentDate);
    Task UpdateStatusAsync(ComplaintStatus status);
    Task AddActionTakenAsync(string action);
    Task LogCorrespondenceAsync(string direction, string method, string summary, string handledBy);
    Task ResolveComplaintAsync(ResolutionOutcome outcome, string resolutionSummary);
    Task CloseComplaintAsync();
    Task<ComplaintState> GetComplaintAsync();
}
