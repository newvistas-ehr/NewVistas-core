// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient maintainer of the class→patient reverse index. Keyed by patient id.
///
/// On <see cref="RefreshAsync"/> it recomputes the patient's active VA drug-class set
/// from the PSO prescription index (resolving each active drug's full class set via
/// <c>IDrugGrain.GetDrugClassAsync</c>), diffs it against the previously stored set,
/// and adds/removes the patient from the affected <see cref="IDrugClassCohortIndexGrain"/>
/// shards. PharmacyGrain calls this on every prescription lifecycle write so the
/// reverse index stays live.
/// </summary>
public interface IPatientDrugClassIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Recomputes the patient's active drug-class membership and propagates the change
    /// to the class cohort shards. Idempotent and safe to call repeatedly.
    /// </summary>
    Task RefreshAsync();

    /// <summary>Returns the patient's current active VA drug-class codes (upper-cased).</summary>
    Task<List<string>> GetActiveClassCodesAsync();
}
