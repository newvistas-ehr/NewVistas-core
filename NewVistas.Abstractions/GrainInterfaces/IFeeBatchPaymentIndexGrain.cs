// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all fee basis batch payments.
/// Grain key: "FEE-BATCH-IDX"
/// </summary>
public interface IFeeBatchPaymentIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new entry or updates an existing one (matched by BatchId).</summary>
    Task AddOrUpdateAsync(FeeBatchPaymentIndexEntry entry);

    /// <summary>Returns all batch payment entries.</summary>
    Task<List<FeeBatchPaymentIndexEntry>> GetAllAsync();

    /// <summary>Returns only batches that have not yet been posted.</summary>
    Task<List<FeeBatchPaymentIndexEntry>> GetUnpostedAsync();
}
