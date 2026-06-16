// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for all safety plans.
/// Key pattern: "SP-PLAN-IDX:{patientId}"
/// </summary>
public interface ISafetyPlanIndexGrain : IGrainWithStringKey
{
    Task<List<SafetyPlanSummary>> GetAllPlansAsync();

    Task<SafetyPlanSummary?> GetActivePlanAsync();

    Task UpsertPlanAsync(SafetyPlanSummary summary);

    Task RemovePlanAsync(string planId);
}
