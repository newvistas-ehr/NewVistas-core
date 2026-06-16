// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class AutoEligibilityDeterminationGrain : Grain, IAutoEligibilityDeterminationGrain
{
    private readonly IPersistentState<AutoEligibilityDeterminationState> _state;

    public AutoEligibilityDeterminationGrain(
        [PersistentState("autoEligibilityDeterminationState", "autoEligibilityDeterminationStore")]
        IPersistentState<AutoEligibilityDeterminationState> state)
    {
        _state = state;
    }

    public Task<AutoEligibilityDeterminationState> GetAsync() => Task.FromResult(_state.State);

    public async Task<AutoEligibilityDeterminationState> DetermineAsync(
        string patientId, string enrollmentStatus, string? priorityGroup, string? prioritySubgroup,
        bool meansTestRequired, bool meansTestCompleted, string? meansTestId,
        decimal? adjustedIncome, decimal? gmtThreshold, string? copayTestResult,
        bool isServiceConnected50Plus, int? serviceConnectedPercent,
        bool receivesVaPension, bool isCatastrophicallyDisabled,
        bool isFormerPOW, bool isPurpleHeart,
        string? determinedByUserId, string? determinedByUserName)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.DeterminationId            = this.GetPrimaryKeyString();
        _state.State.PatientId                  = patientId;
        _state.State.DeterminationDate          = now;
        _state.State.EnrollmentStatus           = enrollmentStatus;
        _state.State.PriorityGroup              = priorityGroup;
        _state.State.PrioritySubgroup           = prioritySubgroup;
        _state.State.MeansTestRequired          = meansTestRequired;
        _state.State.MeansTestCompleted         = meansTestCompleted;
        _state.State.MeansTestId                = meansTestId;
        _state.State.AdjustedIncome             = adjustedIncome;
        _state.State.GmtThreshold               = gmtThreshold;
        _state.State.CopayTestResult            = copayTestResult;
        _state.State.IsServiceConnected50Plus   = isServiceConnected50Plus;
        _state.State.ServiceConnectedPercent    = serviceConnectedPercent;
        _state.State.ReceivesVaPension          = receivesVaPension;
        _state.State.IsCatastrophicallyDisabled = isCatastrophicallyDisabled;
        _state.State.IsFormerPOW                = isFormerPOW;
        _state.State.IsPurpleHeart              = isPurpleHeart;
        _state.State.DeterminedByUserId         = determinedByUserId;
        _state.State.DeterminedByUserName       = determinedByUserName;
        _state.State.CreatedDate                = now;
        _state.State.LastModifiedDate           = now;

        // ─── Eligibility determination rules (VistA DG priority group logic) ──
        List<string> factors = new();
        EligibilityDeterminationResult result;
        bool copayRequired = true;
        string? exemptionReason = null;
        string? assignedPriority = priorityGroup;

        // Check enrollment status first
        if (enrollmentStatus == "Rejected" || enrollmentStatus == "Closed")
        {
            result = EligibilityDeterminationResult.NotEligible;
            factors.Add($"Enrollment status is {enrollmentStatus}");
        }
        // Priority Group 1: SC 50%+ — always exempt
        else if (isServiceConnected50Plus || (serviceConnectedPercent.HasValue && serviceConnectedPercent.Value >= 50))
        {
            result = EligibilityDeterminationResult.Exempt;
            copayRequired = false;
            exemptionReason = "SC_50_PLUS";
            assignedPriority = "1";
            factors.Add($"Service-connected disability {serviceConnectedPercent ?? 50}% (>=50%)");
        }
        // Former POW — exempt
        else if (isFormerPOW)
        {
            result = EligibilityDeterminationResult.Exempt;
            copayRequired = false;
            exemptionReason = "FORMER_POW";
            assignedPriority = "3";
            factors.Add("Former Prisoner of War");
        }
        // Purple Heart — exempt
        else if (isPurpleHeart)
        {
            result = EligibilityDeterminationResult.Exempt;
            copayRequired = false;
            exemptionReason = "PURPLE_HEART";
            assignedPriority = "3";
            factors.Add("Purple Heart recipient");
        }
        // Catastrophically disabled — exempt
        else if (isCatastrophicallyDisabled)
        {
            result = EligibilityDeterminationResult.Exempt;
            copayRequired = false;
            exemptionReason = "CATASTROPHICALLY_DISABLED";
            assignedPriority = "4";
            factors.Add("Catastrophically disabled");
        }
        // VA pension — exempt
        else if (receivesVaPension)
        {
            result = EligibilityDeterminationResult.Exempt;
            copayRequired = false;
            exemptionReason = "VA_PENSION";
            assignedPriority = "5";
            factors.Add("Receives VA pension");
        }
        // Means test based determination
        else if (meansTestRequired && !meansTestCompleted)
        {
            result = EligibilityDeterminationResult.Pending;
            factors.Add("Means test required but not completed");
        }
        else if (copayTestResult == "EXEMPT")
        {
            result = EligibilityDeterminationResult.Exempt;
            copayRequired = false;
            exemptionReason = "MEANS_TEST_EXEMPT";
            factors.Add("Means test result: EXEMPT");
        }
        else if (copayTestResult == "COPAY_REQUIRED" || copayTestResult == "FIRST_PARTY")
        {
            result = EligibilityDeterminationResult.CopayRequired;
            copayRequired = true;
            factors.Add($"Means test result: {copayTestResult}");

            if (adjustedIncome.HasValue && gmtThreshold.HasValue)
            {
                factors.Add($"Adjusted income ${adjustedIncome:F2} vs GMT threshold ${gmtThreshold:F2}");
                if (adjustedIncome.Value > gmtThreshold.Value)
                {
                    assignedPriority = "8";
                    factors.Add("Income above GMT threshold — Priority Group 8");
                }
                else
                {
                    assignedPriority = "7";
                    factors.Add("Income below GMT threshold — Priority Group 7");
                }
            }
        }
        // Income-based ineligibility
        else if (adjustedIncome.HasValue && gmtThreshold.HasValue && adjustedIncome.Value > gmtThreshold.Value * 1.5m)
        {
            result = EligibilityDeterminationResult.NotEligibleIncome;
            factors.Add($"Adjusted income ${adjustedIncome:F2} exceeds 150% of GMT ${gmtThreshold.Value * 1.5m:F2}");
        }
        else
        {
            result = EligibilityDeterminationResult.CopayRequired;
            factors.Add("Default determination: copay required");
        }

        _state.State.Result                      = result;
        _state.State.CopayRequired               = copayRequired;
        _state.State.CopayExemptionReason         = exemptionReason;
        _state.State.AssignedPriorityGroup        = assignedPriority;
        _state.State.DeterminationFactors         = factors;

        await _state.WriteStateAsync();
        return _state.State;
    }
}
