// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Index grain for all Home Telehealth alerts belonging to a single patient.
/// Grain key: "HT-ALERT-IDX:{patientId}"
/// </summary>
public interface IHomeTelehealthAlertIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds an alert summary to this patient's index.</summary>
    Task AddAsync(HtAlertIndexEntry entry);

    /// <summary>
    /// Returns alert summaries, optionally filtered by status.
    /// Results are ordered most-recent-first.
    /// </summary>
    Task<List<HtAlertIndexEntry>> GetAsync(HtAlertStatus? status);

    /// <summary>Updates the status of an alert in the index.</summary>
    Task UpdateStatusAsync(string alertId, HtAlertStatus status);
}
