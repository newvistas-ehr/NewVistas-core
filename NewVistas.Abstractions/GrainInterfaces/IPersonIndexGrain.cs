// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton directory of Persons (ADR-002) for candidate lookup. Key: "PERSON-INDEX:DEFAULT".
/// Populated as Persons register / gain roles. (Fuzzy matching against existing UNLINKED patients and
/// staff — to suggest links — reuses <c>IMpiSearchGrain</c> + the provider directory in a later phase.)
/// </summary>
public interface IPersonIndexGrain : IGrainWithStringKey
{
    Task UpsertAsync(PersonIndexEntry entry);
    Task RemoveAsync(string personId);
    Task<List<PersonIndexEntry>> GetAllAsync();
    /// <summary>Case-insensitive last-name / "Last,First" prefix match.</summary>
    Task<List<PersonIndexEntry>> SearchByNameAsync(string namePrefix);
    /// <summary>Persons flagged employee-patient (both a patient- and a staff-role) — sensitive.</summary>
    Task<List<PersonIndexEntry>> GetEmployeePatientsAsync();
}
