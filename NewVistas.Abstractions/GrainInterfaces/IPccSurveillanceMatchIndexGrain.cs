// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton PCC Surveillance match index — dashboard for encounter matches.
/// Key: "PCC-SURV-MATCH-IDX"
/// </summary>
public interface IPccSurveillanceMatchIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.PccSurveillanceMatchIndexEntry>> GetAllAsync();
    Task<List<GrainStates.PccSurveillanceMatchIndexEntry>> GetByStatusAsync(
        GrainStates.PccSurveillanceMatchStatus status);
    Task<List<GrainStates.PccSurveillanceMatchIndexEntry>> GetByConditionAsync(string conditionName);
    Task AddEntryAsync(GrainStates.PccSurveillanceMatchIndexEntry entry);
    Task UpdateStatusAsync(string matchId, GrainStates.PccSurveillanceMatchStatus status);
}
