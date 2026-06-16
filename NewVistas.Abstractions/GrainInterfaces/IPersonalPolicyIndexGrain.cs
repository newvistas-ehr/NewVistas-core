// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all personal insurance policies (File #355.7).
/// Provides fast policy list retrieval without activating individual policy grains.
/// Grain key: "IB-POLICY-IDX:{patientId}"
/// </summary>
public interface IPersonalPolicyIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a policy entry in the index (upsert by PolicyId).</summary>
    Task AddOrUpdateAsync(GrainStates.PersonalPolicyIndexEntry entry);

    /// <summary>Returns all policy entries for this patient.</summary>
    Task<List<GrainStates.PersonalPolicyIndexEntry>> GetAllAsync();

    /// <summary>Returns only active policy entries for this patient.</summary>
    Task<List<GrainStates.PersonalPolicyIndexEntry>> GetActiveAsync();

    /// <summary>Removes a policy entry from the index by policy ID.</summary>
    Task RemoveAsync(string policyId);
}
