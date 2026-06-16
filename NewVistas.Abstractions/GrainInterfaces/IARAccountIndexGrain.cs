// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all AR account summaries for fast listing and balance aggregation.
/// Grain key: "AR-ACCT-IDX:{patientId}"
/// </summary>
public interface IARAccountIndexGrain : IGrainWithStringKey
{
    /// <summary>Upserts an AR account summary entry in the index.</summary>
    Task AddOrUpdateAsync(ARAccountIndexEntry entry);

    /// <summary>Returns all AR account summaries for this patient.</summary>
    Task<List<ARAccountIndexEntry>> GetAllAsync();

    /// <summary>
    /// Returns AR account summaries with Active, OnPaymentPlan, or InCollection status.
    /// </summary>
    Task<List<ARAccountIndexEntry>> GetActiveAsync();

    /// <summary>Returns AR account summaries filtered by <paramref name="status"/> (enum name string).</summary>
    Task<List<ARAccountIndexEntry>> GetByStatusAsync(string status);
}
