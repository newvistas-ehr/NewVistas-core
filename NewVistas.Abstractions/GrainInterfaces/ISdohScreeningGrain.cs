// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// One SDOH screening event (grain key <c>SDOH:{guid}</c>). Records the per-domain answers and the
/// computed positive-domain findings, and tracks the closed-loop actions taken (Z-code added /
/// referral created).
/// </summary>
public interface ISdohScreeningGrain : IGrainWithStringKey
{
    /// <summary>Records the screening and computes its positive-domain findings via the catalog.</summary>
    Task RecordScreeningAsync(string patientId, string instrumentName, List<SdohScreeningResponse> responses, string recordedBy);

    /// <summary>Records a closed-loop action taken for a positive domain (Z-code added / referral created).</summary>
    Task RecordActionAsync(SdohDomain domain, SdohActionType actionType, string targetId, string byUser);

    Task<SdohScreeningState> GetAsync();
}

/// <summary>Per-patient index of SDOH screenings (grain key <c>SDOH-IDX:{patientId}</c>).</summary>
public interface ISdohScreeningIndexGrain : IGrainWithStringKey
{
    Task AddEntryAsync(SdohScreeningSummary summary);
    Task<List<SdohScreeningSummary>> GetAllAsync();
}

/// <summary>
/// Reverse-index shard: patients with a positive screen for one SDOH domain (grain key
/// <c>SDOH-COHORT:{domain}</c>). Population reporting for value-based / community-health.
/// </summary>
public interface ISdohCohortIndexGrain : IGrainWithStringKey
{
    Task AddPatientAsync(string patientId);
    Task<List<string>> GetPatientsAsync();
    Task<bool> ContainsAsync(string patientId);
    Task<int> GetCountAsync();
}
