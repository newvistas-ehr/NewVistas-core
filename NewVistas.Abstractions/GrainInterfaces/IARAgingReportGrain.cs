// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for generating and storing AR aging reports.
/// Provides financial reporting, AR aging analysis, and revenue cycle metrics.
/// Maps to VistA PRCA AR aging reports (RCRP*.m).
/// Grain key: "AR-AGING:{patientId}" (per-patient)
/// </summary>
public interface IARAgingReportGrain : IGrainWithStringKey
{
    /// <summary>Returns the current aging report state.</summary>
    Task<ARAgingReportState> GetAsync();

    /// <summary>
    /// Generates a new aging report from the patient's current AR accounts.
    /// Classifies each account into aging buckets and computes summary metrics.
    /// </summary>
    Task GenerateAsync(
        string patientId,
        List<AgingAccountDetail> accountDetails,
        RevenueCycleMetrics metrics,
        string? generatedByUserId,
        string? generatedByUserName);

    /// <summary>
    /// Returns aging bucket summaries only (lightweight query).
    /// </summary>
    Task<List<AgingBucketSummary>> GetBucketSummariesAsync();

    /// <summary>
    /// Returns revenue cycle metrics only (lightweight query).
    /// </summary>
    Task<RevenueCycleMetrics?> GetMetricsAsync();

    /// <summary>
    /// Returns account details filtered by aging bucket.
    /// </summary>
    Task<List<AgingAccountDetail>> GetAccountsByBucketAsync(AgingBucket bucket);
}
