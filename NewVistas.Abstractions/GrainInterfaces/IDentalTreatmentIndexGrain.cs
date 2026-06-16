// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all dental treatments, enabling efficient listing
/// and filtering without loading individual treatment grains.
/// Grain key: "DENTAL-TX-IDX:{patientId}".
/// </summary>
public interface IDentalTreatmentIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all treatment index entries for this patient, newest first.</summary>
    Task<List<DentalTreatmentIndexEntry>> GetAllAsync();

    /// <summary>Returns only treatments with the specified status.</summary>
    Task<List<DentalTreatmentIndexEntry>> GetByStatusAsync(DentalTreatmentStatus status);

    /// <summary>Adds a new treatment entry to the index.</summary>
    Task AddEntryAsync(DentalTreatmentIndexEntry entry);

    /// <summary>Updates the status of an existing index entry.</summary>
    Task UpdateEntryStatusAsync(string treatmentId, DentalTreatmentStatus status);
}
