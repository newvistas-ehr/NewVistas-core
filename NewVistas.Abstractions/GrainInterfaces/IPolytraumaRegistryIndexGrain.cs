// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton polytrauma registry index grain — key: "PT-REGISTRY-IDX"
/// Cross-patient registry of all polytrauma enrollments for reporting.
/// </summary>
public interface IPolytraumaRegistryIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all registry entries ordered by RegistrationDate descending.</summary>
    Task<List<PolytraumaRegistrySummaryEntry>> GetAllPatientsAsync();

    /// <summary>Returns only Active enrollments.</summary>
    Task<List<PolytraumaRegistrySummaryEntry>> GetActivePatientAsync();

    Task<List<PolytraumaRegistrySummaryEntry>> GetPatientsByStatusAsync(PolytraumaStatus status);

    Task UpsertPatientAsync(PolytraumaRegistrySummaryEntry entry);

    Task RemovePatientAsync(string patientId);
}
