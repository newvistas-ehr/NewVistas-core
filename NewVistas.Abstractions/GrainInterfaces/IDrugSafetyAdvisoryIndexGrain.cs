// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of all drug safety advisories for list/dashboard display and
/// drug-class lookup. Grain key: "DSA-INDEX". Maintained by the advisory grain on
/// every lifecycle write.
/// </summary>
public interface IDrugSafetyAdvisoryIndexGrain : IGrainWithStringKey
{
    /// <summary>Inserts or updates an advisory summary.</summary>
    Task UpsertAsync(DrugSafetyAdvisorySummary summary);

    /// <summary>Returns advisories in the Active lifecycle state.</summary>
    Task<List<DrugSafetyAdvisorySummary>> GetActiveAsync();

    /// <summary>Returns all advisory summaries.</summary>
    Task<List<DrugSafetyAdvisorySummary>> GetAllAsync();

    /// <summary>
    /// Returns Active advisories whose target classes include the given VA drug class code
    /// (case-insensitive). Used to surface advisories relevant to a drug/patient.
    /// </summary>
    Task<List<DrugSafetyAdvisorySummary>> GetActiveByDrugClassAsync(string drugClassCode);
}
