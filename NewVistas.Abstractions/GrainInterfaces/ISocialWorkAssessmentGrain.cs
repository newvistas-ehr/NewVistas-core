// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Social Work Assessment Grain — VistA File #707 (SOCIAL WORK ASSESSMENT).
/// Key: "SW-ASSESSMENT:{guid}"
///
/// Mirrors SWRPATCH.m — stores psychosocial assessments, risk levels,
/// housing/employment data, and discharge planning findings.
/// </summary>
public interface ISocialWorkAssessmentGrain : IGrainWithStringKey
{
    Task<GrainStates.SocialWorkAssessmentState> GetAsync();

    Task CreateAsync(
        string patientId,
        GrainStates.SocialWorkAssessmentType assessmentType,
        DateTime assessmentDate,
        string? socialWorkerId,
        string? socialWorkerName,
        GrainStates.SocialWorkRiskLevel riskLevel,
        string? housingStatus,
        string? employmentStatus,
        string? socialSupport,
        string? financialStressors,
        string? substanceUseHistory,
        bool? abuseConcernsIdentified,
        bool? safetyPlanInPlace,
        DateTime? anticipatedDischargeDate,
        string? dischargeDisposition,
        string? dischargePlan,
        List<string>? dischargeBarriers,
        string? recommendations,
        string? notes,
        string? locationId,
        string? locationName);

    Task CompleteAsync(
        DateTime completedDate,
        string? recommendations,
        string? notes);

    Task CloseAsync(string reason);

    Task UpdateRiskLevelAsync(GrainStates.SocialWorkRiskLevel riskLevel);
}
