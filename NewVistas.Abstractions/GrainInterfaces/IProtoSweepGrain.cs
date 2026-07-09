// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton screening-sweep coordinator (grain key <c>PROTO-SWEEP</c>). A sweep is an explicit,
/// EPI-gated action — it walks the active patient population (paged) and records each patient's
/// evaluation against a proto-condition, or re-clusters an explicit patient list (the split path).
/// No timers in v1.
/// </summary>
public interface IProtoSweepGrain : IGrainWithStringKey
{
    /// <summary>Screens up to <paramref name="maxPatients"/> active patients against the proto and records results.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task<ProtoSweepRun> SweepProtoAsync(string protoConditionId, int? maxPatients, string runBy);

    /// <summary>Screens an explicit patient list against the proto (targeted re-clustering after a split).</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task<ProtoSweepRun> SweepPatientsAsync(string protoConditionId, List<string> patientIds, string runBy);

    /// <summary>Recent sweep runs, newest first (open read).</summary>
    Task<List<ProtoSweepRun>> GetRecentRunsAsync();
}
