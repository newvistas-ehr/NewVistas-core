// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all vital measurement grain keys.
/// Stores keys sorted by datetime descending for efficient range queries.
/// Grain Key: patient ID (same key as IPatientGrain).
///
/// The index supports the "load more" pattern: the patient grain holds the
/// last N vitals (hot cache), and this index provides the full history
/// for date-range queries and paginated retrieval.
/// </summary>
public interface IPatientVitalIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds a vital grain key to the index. Keys are maintained sorted by datetime descending.
    /// </summary>
    Task AddVitalKeyAsync(string vitalGrainKey, DateTime dateTimeTaken, string vitalType);

    /// <summary>
    /// Removes a vital grain key from the index.
    /// </summary>
    Task RemoveVitalKeyAsync(string vitalGrainKey);

    /// <summary>
    /// Gets all vital keys, sorted by datetime descending.
    /// </summary>
    Task<List<GrainStates.VitalIndexEntry>> GetAllKeysAsync();

    /// <summary>
    /// Gets vital keys for a specific date range, sorted by datetime descending.
    /// </summary>
    Task<List<GrainStates.VitalIndexEntry>> GetKeysByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>
    /// Gets vital keys before a given date, sorted by datetime descending.
    /// </summary>
    Task<List<GrainStates.VitalIndexEntry>> GetKeysBeforeDateAsync(DateTime before, int maxCount);

    /// <summary>
    /// Gets vital keys filtered by vital type and date range.
    /// </summary>
    Task<List<GrainStates.VitalIndexEntry>> GetKeysByTypeAndDateRangeAsync(
        string vitalType, DateTime from, DateTime to);

    /// <summary>
    /// Gets the total count of vitals in the index.
    /// </summary>
    Task<int> GetCountAsync();
}
