// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A single drug safety advisory (e.g., the 2010 FDA PPI/fracture communication).
/// Authored and reviewed centrally, then dispatched by individual providers to
/// their affected patients. Keyed by a stable advisory id.
/// </summary>
public interface IDrugSafetyAdvisoryGrain : IGrainWithStringKey
{
    /// <summary>Creates or replaces the advisory content. Preserves CreatedDate/By.</summary>
    Task SaveAsync(DrugSafetyAdvisoryState advisory);

    /// <summary>Returns the full advisory state.</summary>
    Task<DrugSafetyAdvisoryState> GetAsync();

    /// <summary>Releases a Draft advisory for provider dispatch.</summary>
    Task ActivateAsync();

    /// <summary>Withdraws an advisory; it can no longer be dispatched.</summary>
    Task RetireAsync();

    /// <summary>
    /// Dispatches the advisory to the given patients using the provider's (optionally
    /// edited) <paramref name="finalMessage"/>. Records a verbatim receipt on each
    /// patient's record. Patients already reached by a prior dispatch are skipped so
    /// no one is double-warned. The advisory must be Active.
    /// </summary>
    Task<AdvisoryDispatchResult> DispatchAsync(
        string finalMessage,
        List<string> patientIds,
        string providerId,
        string providerName,
        AdvisoryChannel channel);

    /// <summary>True if this advisory has already been dispatched to the patient.</summary>
    Task<bool> HasReachedAsync(string patientId);
}
