// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Stores detailed lab results, partitioned by 6-month time window.
/// Partitioning ensures accessing recent results doesn't activate grains holding old data.
///
/// Grain Key: PatientLabBatch/{patientIcn}/{year}-{half}
/// Where {half} is H1 (Jan-Jun) or H2 (Jul-Dec).
/// </summary>
public interface IPatientLabBatch : IGrainWithStringKey
{
    /// <summary>
    /// Store a new lab result in this batch. Inserts in date-descending order.
    /// </summary>
    Task AddResult(LabResultDetail result);

    /// <summary>
    /// Get all results in this time partition.
    /// </summary>
    Task<IReadOnlyList<LabResultDetail>> GetAllResults();

    /// <summary>
    /// Get results filtered by LOINC code within this partition.
    /// </summary>
    Task<IReadOnlyList<LabResultDetail>> GetResultsByType(string loincCode);

    /// <summary>
    /// Get results filtered by date range within this partition.
    /// </summary>
    Task<IReadOnlyList<LabResultDetail>> GetResultsByDateRange(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Get results filtered by panel name (e.g., "CBC", "CMP").
    /// </summary>
    Task<IReadOnlyList<LabResultDetail>> GetResultsByPanel(string panelName);
}
