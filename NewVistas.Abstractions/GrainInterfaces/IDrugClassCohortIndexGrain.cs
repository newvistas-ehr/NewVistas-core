// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Reverse-index shard for one VA drug class — the set of patients currently on a
/// medication in that class. Grain key: the upper-cased class code (e.g., "GA301").
///
/// Maintained by <see cref="IPatientDrugClassIndexGrain"/>; queried for safety-advisory
/// cohort resolution ("which of my patients are on a PPI?").
/// </summary>
public interface IDrugClassCohortIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a patient to this class cohort (idempotent).</summary>
    Task AddPatientAsync(string patientId);

    /// <summary>Removes a patient from this class cohort (idempotent).</summary>
    Task RemovePatientAsync(string patientId);

    /// <summary>Returns all patient ids currently in this class cohort.</summary>
    Task<List<string>> GetPatientsAsync();

    /// <summary>True if the patient is currently in this class cohort.</summary>
    Task<bool> ContainsAsync(string patientId);

    /// <summary>Number of patients currently in this class cohort.</summary>
    Task<int> GetCountAsync();
}
