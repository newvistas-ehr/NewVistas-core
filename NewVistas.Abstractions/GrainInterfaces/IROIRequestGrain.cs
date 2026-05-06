// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single release of information request.
/// VistA File #195 (RELEASE OF INFORMATION). ROIS.m, ROI.m
/// </summary>
public interface IROIRequestGrain : IGrainWithStringKey
{
    Task SubmitRequestAsync(
        string patientId,
        string patientName,
        DateTime? patientDOB,
        ROIRequestType requestType,
        RequesterType requesterType,
        string requesterName,
        string requesterOrganization,
        string requesterAddress,
        string requesterPhone,
        string requesterFax,
        string requesterEmail,
        string purposeOfRequest,
        List<string> recordsRequested,
        DateTime? dateRangeStart,
        DateTime? dateRangeEnd,
        ROIRequestPriority priority,
        string createdBy);

    Task AssignStaffAsync(string staffId, string staffName);
    Task UpdateAuthorizationAsync(AuthorizationStatus authStatus, DateTime? authDate, DateTime? authExpirationDate);
    Task UpdateStatusAsync(ROIRequestStatus status, string notes);
    Task FulfillRequestAsync(FulfillmentMethod fulfillmentMethod, string notes, int numberOfPages, decimal feeCharged);
    Task DenyRequestAsync(string denialReason);
    Task<ROIRequestState> GetRequestAsync();
}
