// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of EDI 276/277 claim status inquiries.
/// Grain key: "CSI-IDX:{patientId}"
/// </summary>
public interface IClaimStatusInquiryIndexGrain : IGrainWithStringKey
{
    Task<List<ClaimStatusInquiryIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(ClaimStatusInquiryIndexEntry entry);
    Task<List<ClaimStatusInquiryIndexEntry>> GetByClaimAsync(string claimId);
}
