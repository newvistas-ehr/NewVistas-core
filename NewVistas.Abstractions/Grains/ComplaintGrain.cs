// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ComplaintGrain : Grain, IComplaintGrain
{
    private readonly IPersistentState<ComplaintState> _state;

    public ComplaintGrain(
        [PersistentState("paComplaintState", "paComplaintStore")] IPersistentState<ComplaintState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ComplaintId))
            _state.State.ComplaintId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task FileComplaintAsync(
        string patientId, string patientName, DateTime complaintDate,
        ComplaintType complaintType, ComplaintCategory category, ComplaintPriority priority,
        InquirySource source, string narrativeDescription, string specificConcern,
        string departmentInvolved, string reporterName, string reporterRelationship,
        bool isConfidential, string createdBy)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ComplaintDate = complaintDate;
        _state.State.ReceivedDate = DateTime.UtcNow;
        _state.State.ComplaintType = complaintType;
        _state.State.Category = category;
        _state.State.Priority = priority;
        _state.State.Status = ComplaintStatus.Received;
        _state.State.Source = source;
        _state.State.NarrativeDescription = narrativeDescription;
        _state.State.SpecificConcern = specificConcern;
        _state.State.DepartmentInvolved = departmentInvolved;
        _state.State.ReporterName = reporterName;
        _state.State.ReporterRelationship = reporterRelationship;
        _state.State.IsConfidential = isConfidential;
        _state.State.CreatedBy = createdBy;
        // Due dates based on priority: Immediate=24h, Urgent=7d, Routine=30d
        _state.State.AcknowledgmentDue = priority == ComplaintPriority.Routine
            ? DateTime.UtcNow.AddDays(3)
            : DateTime.UtcNow.AddDays(1);
        _state.State.ResponseDue = priority switch
        {
            ComplaintPriority.Immediate => DateTime.UtcNow.AddDays(1),
            ComplaintPriority.Urgent    => DateTime.UtcNow.AddDays(7),
            _                           => DateTime.UtcNow.AddDays(30)
        };
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignAdvocateAsync(string advocateId, string advocateName)
    {
        _state.State.AssignedAdvocateId = advocateId;
        _state.State.AssignedAdvocateName = advocateName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeComplaintAsync(DateTime acknowledgmentDate)
    {
        _state.State.AcknowledgmentDate = acknowledgmentDate;
        _state.State.Status = ComplaintStatus.Acknowledged;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(ComplaintStatus status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddActionTakenAsync(string action)
    {
        if (!_state.State.ActionsTaken.Contains(action))
            _state.State.ActionsTaken.Add(action);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LogCorrespondenceAsync(string direction, string method, string summary, string handledBy)
    {
        _state.State.CorrespondenceLog.Add(new ComplaintCorrespondence
        {
            CorrespondenceId = Guid.NewGuid().ToString(),
            Date = DateTime.UtcNow,
            Direction = direction,
            Method = method,
            Summary = summary,
            HandledBy = handledBy
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveComplaintAsync(ResolutionOutcome outcome, string resolutionSummary)
    {
        _state.State.Outcome = outcome;
        _state.State.ResolutionSummary = resolutionSummary;
        _state.State.Status = ComplaintStatus.Resolved;
        _state.State.ResolvedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseComplaintAsync()
    {
        _state.State.Status = ComplaintStatus.Closed;
        _state.State.ClosedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<ComplaintState> GetComplaintAsync() => Task.FromResult(_state.State);
}
