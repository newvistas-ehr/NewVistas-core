// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// PCC Surveillance Configuration Grain — RPMS APCSB.m / APCSA.m.
/// Key: "PCC-SURV-CONFIG:{configId}"
///
/// Defines encounter-level surveillance criteria for a reportable condition.
/// Additive feature grain per Site Flavor Architecture (Option 4).
/// </summary>
public interface IPccSurveillanceConfigGrain : IGrainWithStringKey
{
    Task<GrainStates.PccSurveillanceConfigState> GetAsync();

    Task SaveAsync(
        string conditionName,
        GrainStates.PccEncounterClassification classification,
        List<GrainStates.PccSurveillanceCriterion>? criteria,
        List<GrainStates.PccVisitType>? requiredVisitTypes,
        bool detectComorbidities, bool captureVitals,
        int scanWindowDays,
        List<string>? jurisdictions, string reportingTimeframe,
        bool isActive);

    Task AddCriterionAsync(GrainStates.PccSurveillanceCriterion criterion);
    Task SetActiveAsync(bool isActive);
}
