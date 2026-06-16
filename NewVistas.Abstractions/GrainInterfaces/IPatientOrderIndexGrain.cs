// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all order grain keys.
/// Stores keys sorted by StartDate descending for efficient filtering and range queries.
/// Grain Key: patient ID (same key as IPatientGrain).
///
/// Unlike the vital index (write-once), orders change status over their lifecycle
/// (Pending → Active → Completed/Discontinued), so this index supports AddOrUpdate
/// to keep status metadata current without activating individual order grains.
///
/// The index supports the ORWORR AGET filter codes (1=All, 2=Current, 3=Discontinued,
/// 4=Completed/Expired, 5=Expiring, 7=Pending, 11=Unsigned) entirely from index
/// metadata, eliminating the N+1 fan-out problem for filtered order lists.
/// </summary>
public interface IPatientOrderIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds or updates an order entry in the index.
    /// If an entry with the same OrderGrainKey exists, it is replaced.
    /// Entries are maintained sorted by StartDate descending.
    /// </summary>
    Task AddOrUpdateOrderAsync(GrainStates.OrderIndexEntry entry);

    /// <summary>
    /// Removes an order entry from the index.
    /// </summary>
    Task RemoveOrderAsync(string orderGrainKey);

    /// <summary>
    /// Gets all order entries, sorted by StartDate descending.
    /// </summary>
    Task<List<GrainStates.OrderIndexEntry>> GetAllEntriesAsync();

    /// <summary>
    /// Gets order entries for a specific date range, sorted by StartDate descending.
    /// </summary>
    Task<List<GrainStates.OrderIndexEntry>> GetEntriesByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>
    /// Gets order entries before a given date, sorted by StartDate descending.
    /// </summary>
    Task<List<GrainStates.OrderIndexEntry>> GetEntriesBeforeDateAsync(DateTime before, int maxCount);

    /// <summary>
    /// Gets order entries matching an ORWORR AGET filter code.
    /// Filter codes: 1=All, 2=Current, 3=Discontinued, 4=Completed/Expired,
    /// 5=Expiring, 7=Pending, 11=Unsigned.
    /// </summary>
    Task<List<GrainStates.OrderIndexEntry>> GetEntriesByFilterAsync(int filter);

    /// <summary>
    /// Gets the total count of orders in the index.
    /// </summary>
    Task<int> GetCountAsync();
}
