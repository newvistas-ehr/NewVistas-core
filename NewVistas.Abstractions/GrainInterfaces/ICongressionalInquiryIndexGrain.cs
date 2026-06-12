// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide Congressional inquiry index grain.
/// Singleton key: "PA-CONGRESS-IDX".
/// </summary>
public interface ICongressionalInquiryIndexGrain : IGrainWithStringKey
{
    Task UpsertInquiryAsync(CongressionalInquiryIndexEntry entry);
    Task<List<CongressionalInquiryIndexEntry>> GetAllInquiriesAsync();
    Task<List<CongressionalInquiryIndexEntry>> GetPendingInquiriesAsync();
    Task<List<CongressionalInquiryIndexEntry>> GetOverdueInquiriesAsync();
    Task<List<CongressionalInquiryIndexEntry>> GetInquiriesByPatientAsync(string patientId, int maxResults = 50);
}
