// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for automatic eligibility determination based on means test, enrollment, and priority group.
/// Integrates DG/IB data to automatically determine copay obligation and enrollment eligibility.
/// Maps to VistA DG/IB integration (DGENELA.m, IBCNEDE.m).
/// Grain key: "ELIG-DET:{patientId}"
/// </summary>
public interface IAutoEligibilityDeterminationGrain : IGrainWithStringKey
{
    Task<AutoEligibilityDeterminationState> GetAsync();

    Task<AutoEligibilityDeterminationState> DetermineAsync(
        string patientId,
        string enrollmentStatus,
        string? priorityGroup,
        string? prioritySubgroup,
        bool meansTestRequired,
        bool meansTestCompleted,
        string? meansTestId,
        decimal? adjustedIncome,
        decimal? gmtThreshold,
        string? copayTestResult,
        bool isServiceConnected50Plus,
        int? serviceConnectedPercent,
        bool receivesVaPension,
        bool isCatastrophicallyDisabled,
        bool isFormerPOW,
        bool isPurpleHeart,
        string? determinedByUserId,
        string? determinedByUserName);
}
