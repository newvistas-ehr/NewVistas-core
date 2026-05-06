// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for a single TOP offset matching record.
/// Receives offset payments from Treasury and matches them to AR accounts.
/// Maps to VistA PRCA TOP processing (RCTP*.m, RCTOP*.m).
/// Grain key: "TOP-MATCH:{guid}"
/// </summary>
public interface ITopMatchingGrain : IGrainWithStringKey
{
    Task<TopMatchingState> GetAsync();

    Task<string> RecordOffsetAsync(
        string treasuryTransactionId, string taxpayerIdNumber, string treasuryPatientName,
        decimal offsetAmount, string offsetSource, DateTime offsetReceivedDate, string? notes);

    Task MatchToAccountAsync(
        string matchedPatientId, string matchedARAccountId, string? matchedTopReferralId,
        decimal appliedAmount, string? processedByUserId, string? processedByUserName);

    Task MarkUnmatchedAsync(List<string> reasons, string? processedByUserId);

    Task RejectAsync(List<string> reasons, string? processedByUserId);
}
