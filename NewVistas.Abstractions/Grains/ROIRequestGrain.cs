// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ROIRequestGrain : Grain, IROIRequestGrain
{
    private readonly IPersistentState<ROIRequestState> _state;

    public ROIRequestGrain(
        [PersistentState("roiRequestState", "roiRequestStore")] IPersistentState<ROIRequestState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.RequestId))
            _state.State.RequestId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SubmitRequestAsync(
        string patientId, string patientName, DateTime? patientDOB,
        ROIRequestType requestType, RequesterType requesterType,
        string requesterName, string requesterOrganization, string requesterAddress,
        string requesterPhone, string requesterFax, string requesterEmail,
        string purposeOfRequest, List<string> recordsRequested,
        DateTime? dateRangeStart, DateTime? dateRangeEnd,
        ROIRequestPriority priority, string createdBy)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.PatientDOB = patientDOB;
        _state.State.ReceivedDate = DateTime.UtcNow;
        _state.State.RequestType = requestType;
        _state.State.RequesterType = requesterType;
        _state.State.RequesterName = requesterName;
        _state.State.RequesterOrganization = requesterOrganization;
        _state.State.RequesterAddress = requesterAddress;
        _state.State.RequesterPhone = requesterPhone;
        _state.State.RequesterFax = requesterFax;
        _state.State.RequesterEmail = requesterEmail;
        _state.State.PurposeOfRequest = purposeOfRequest;
        _state.State.RecordsRequested = recordsRequested;
        _state.State.DateRangeStart = dateRangeStart;
        _state.State.DateRangeEnd = dateRangeEnd;
        _state.State.Priority = priority;
        _state.State.Status = ROIRequestStatus.Received;
        _state.State.CreatedBy = createdBy;
        // HIPAA mandates 30-day response; authorization check required for non-patient requesters
        _state.State.DueDate = priority == ROIRequestPriority.Urgent
            ? DateTime.UtcNow.AddDays(3)
            : DateTime.UtcNow.AddDays(30);
        _state.State.AuthorizationStatus = requesterType == RequesterType.Patient
            ? AuthorizationStatus.NotRequired
            : AuthorizationStatus.Pending;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignStaffAsync(string staffId, string staffName)
    {
        _state.State.AssignedStaffId = staffId;
        _state.State.AssignedStaffName = staffName;
        _state.State.Status = ROIRequestStatus.Acknowledged;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateAuthorizationAsync(AuthorizationStatus authStatus, DateTime? authDate, DateTime? authExpirationDate)
    {
        _state.State.AuthorizationStatus = authStatus;
        _state.State.AuthorizationDate = authDate;
        _state.State.AuthorizationExpirationDate = authExpirationDate;
        if (authStatus == AuthorizationStatus.Received && _state.State.Status == ROIRequestStatus.PendingAuthorization)
            _state.State.Status = ROIRequestStatus.InProcess;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(ROIRequestStatus status, string notes)
    {
        _state.State.Status = status;
        if (!string.IsNullOrEmpty(notes))
            _state.State.ProcessingNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FulfillRequestAsync(FulfillmentMethod fulfillmentMethod, string notes, int numberOfPages, decimal feeCharged)
    {
        _state.State.FulfillmentMethod = fulfillmentMethod;
        _state.State.FulfillmentNotes = notes;
        _state.State.NumberOfPagesFulfilled = numberOfPages;
        _state.State.FeeCharged = feeCharged;
        _state.State.FulfillmentDate = DateTime.UtcNow;
        _state.State.Status = ROIRequestStatus.Fulfilled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DenyRequestAsync(string denialReason)
    {
        _state.State.DenialReason = denialReason;
        _state.State.Status = ROIRequestStatus.Denied;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<ROIRequestState> GetRequestAsync() => Task.FromResult(_state.State);
}
