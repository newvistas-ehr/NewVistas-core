// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Index grain for all Home Telehealth readings belonging to a single patient.
/// Grain key: "HT-READING-IDX:{patientId}"
/// </summary>
public interface IHomeTelehealthReadingIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a reading summary to this patient's index.</summary>
    Task AddAsync(HtReadingIndexEntry entry);

    /// <summary>
    /// Returns readings, optionally filtered by measurement type and time window.
    /// Results are ordered most-recent-first.
    /// </summary>
    Task<List<HtReadingIndexEntry>> GetAsync(HtMeasurementType? measurementType, int? days, int maxResults);

    /// <summary>Updates the IsReviewed flag for a specific reading.</summary>
    Task MarkReviewedAsync(string readingId);
}
