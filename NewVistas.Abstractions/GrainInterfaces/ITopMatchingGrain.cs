// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
