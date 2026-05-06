// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
