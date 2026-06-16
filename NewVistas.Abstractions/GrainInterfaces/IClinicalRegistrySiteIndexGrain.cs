// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-wide cross-registry index grain — key: "CCR-SITE-IDX"
/// Maintains a combined view of all enrollments across all registry types for dashboards and reporting.
/// </summary>
public interface IClinicalRegistrySiteIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all entries across all registry types, newest enrolled first.</summary>
    Task<List<CCREntrySummary>> GetAllEntriesAsync();

    /// <summary>Returns the most recently enrolled patients across all registry types.</summary>
    Task<List<CCREntrySummary>> GetRecentEnrollmentsAsync(int count);

    Task UpsertEntryAsync(CCREntrySummary entry);

    /// <summary>Removes the entry matching both the patient ID and registry type.</summary>
    Task RemoveEntryAsync(string patientId, RegistryType registryType);
}
