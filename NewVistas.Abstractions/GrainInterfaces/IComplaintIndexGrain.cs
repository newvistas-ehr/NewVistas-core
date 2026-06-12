// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide complaint index grain.
/// Singleton key: "PA-COMPLAINT-IDX".
/// </summary>
public interface IComplaintIndexGrain : IGrainWithStringKey
{
    Task UpsertComplaintAsync(ComplaintIndexEntry entry);
    Task<List<ComplaintIndexEntry>> GetAllComplaintsAsync();
    Task<List<ComplaintIndexEntry>> GetComplaintsByStatusAsync(ComplaintStatus status);
    Task<List<ComplaintIndexEntry>> GetComplaintsByPatientAsync(string patientId, int maxResults = 50);
    Task<List<ComplaintIndexEntry>> GetComplaintsByTypeAsync(ComplaintType complaintType);
    Task<List<ComplaintIndexEntry>> GetOverdueComplaintsAsync();
}
