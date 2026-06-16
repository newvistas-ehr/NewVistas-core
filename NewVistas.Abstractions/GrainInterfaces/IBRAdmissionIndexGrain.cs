// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Admission Index Grain — per-patient index of BR admissions.
///
/// Grain key: "BR-ADMIT-IDX:{patientId}"
/// </summary>
public interface IBRAdmissionIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all admission index entries for the patient.</summary>
    Task<List<BRAdmissionIndexEntry>> GetAllAsync();

    /// <summary>Returns only active (not discharged/cancelled) admissions.</summary>
    Task<List<BRAdmissionIndexEntry>> GetActiveAsync();

    /// <summary>Adds a new admission entry to the index.</summary>
    Task AddAsync(BRAdmissionIndexEntry entry);

    /// <summary>Updates the status of an existing admission entry.</summary>
    Task UpdateStatusAsync(string admitId, BRAdmissionStatus status);
}
