// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for an EDI 276/277 claim status inquiry.
/// Maps to VistA IB Claim Status (IBCSC*.m) and X12 276/277 transaction sets.
/// Grain key: "CSI-276:{guid}"
/// </summary>
public interface IClaimStatusInquiryGrain : IGrainWithStringKey
{
    Task<ClaimStatusInquiryState> GetAsync();

    Task<string> CreateAsync(
        string patientId, string claimId, string payerId, string payerName,
        decimal? totalBilledAmount, string? initiatedByUserId, string? initiatedByUserName, string? notes);

    Task SubmitAsync(string? traceNumber);

    Task RecordResponseAsync(
        ClaimStatusCategory claimStatusCategory,
        List<ClaimStatusDetail> statusDetails,
        decimal? reportedPaidAmount,
        string? payerMessage);

    Task RecordErrorAsync(List<string> rejectReasonCodes, string? payerMessage);
}
