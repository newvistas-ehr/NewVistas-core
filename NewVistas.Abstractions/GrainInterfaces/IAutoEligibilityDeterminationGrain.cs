// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
