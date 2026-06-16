// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Eligibility;

/// <summary>
/// VA-aligned eligibility policy. Applies 38 CFR §17.36 priority-group rules
/// to a newly-registered patient by invoking
/// <see cref="IAutoEligibilityDeterminationGrain"/> with the veteran-status
/// hints supplied on the <see cref="RegistrationRequest"/>, and applies the
/// resulting priority group + copay flags to the patient's enrollment record.
///
/// Behaviour:
///   • If <see cref="RegistrationRequest.IsVeteran"/> is not true, no enrollment
///     side effects are produced — non-veterans never enter the VA enrollment
///     pipeline.
///   • Otherwise, copies the veteran-info / military-service hints onto the
///     patient grain, runs the determination, and applies the recommended
///     priority group via <see cref="IPatientWorkflowGrain.SetEnrollmentPriorityGroupAsync"/>
///     and (if the determination indicates ineligibility) updates enrollment
///     status accordingly.
///   • Any policy-induced state changes are logged with the patient's ICN so
///     the audit trail can reconstruct who/what set what.
/// </summary>
public sealed class VaRegistrationEligibilityPolicy : IRegistrationEligibilityPolicy
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<VaRegistrationEligibilityPolicy> _logger;

    public VaRegistrationEligibilityPolicy(
        IGrainFactory grainFactory,
        ILogger<VaRegistrationEligibilityPolicy> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task DetermineAndApplyAsync(
        string icn,
        RegistrationRequest request,
        IPatientWorkflowGrain workflow)
    {
        // Non-veterans never enter the VA enrollment pipeline.
        if (request.IsVeteran != true)
        {
            _logger.LogDebug(
                "VA eligibility policy skipped for ICN {Icn}: IsVeteran is not true.",
                icn);
            return;
        }

        // Stamp the veteran info onto the patient state up front so the
        // enrollment record (and any later workflow) can read it directly.
        await workflow.UpdateVeteranInfoAsync(
            veteran: "Y",
            serviceConnectedPercentage: request.ServiceConnectedPercentage,
            eligibilityCode: request.PrimaryEligibilityCode,
            primaryEligibilityCode: request.PrimaryEligibilityCode);

        // Run the auto-determination. The grain implements the full §17.36
        // priority-group ladder; we just feed it the hint flags from the
        // registration request and let it compute the result.
        IAutoEligibilityDeterminationGrain det =
            _grainFactory.GetGrain<IAutoEligibilityDeterminationGrain>($"ELIG-DET:{icn}");

        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            patientId: icn,
            enrollmentStatus: EnrollmentStatus.Unverified.ToString(),
            priorityGroup: null,
            prioritySubgroup: null,
            meansTestRequired: false,
            meansTestCompleted: false,
            meansTestId: null,
            adjustedIncome: null,
            gmtThreshold: null,
            copayTestResult: null,
            isServiceConnected50Plus: (request.ServiceConnectedPercentage ?? 0) >= 50,
            serviceConnectedPercent: request.ServiceConnectedPercentage,
            receivesVaPension: request.ReceivesVaPension ?? false,
            isCatastrophicallyDisabled: request.IsCatastrophicallyDisabled ?? false,
            isFormerPOW: request.IsFormerPow ?? false,
            isPurpleHeart: request.IsPurpleHeart ?? false,
            determinedByUserId: "REGISTRATION",
            determinedByUserName: "Registration auto-determination");

        // Apply the determination to the enrollment record. If a priority
        // group came back, write it; otherwise leave enrollment unverified
        // and let a clerk follow up.
        if (!string.IsNullOrEmpty(result.AssignedPriorityGroup))
        {
            await workflow.SetEnrollmentPriorityGroupAsync(
                priorityGroup: result.AssignedPriorityGroup,
                prioritySubgroup: result.PrioritySubgroup,
                meansTestRequired: result.MeansTestRequired,
                copayExempt: !result.CopayRequired,
                copayExemptionReason: result.CopayExemptionReason);

            _logger.LogInformation(
                "VA eligibility for ICN {Icn}: result={Result}, priorityGroup={PriorityGroup}, copayRequired={CopayRequired}.",
                icn, result.Result, result.AssignedPriorityGroup, result.CopayRequired);
        }
        else
        {
            _logger.LogInformation(
                "VA eligibility for ICN {Icn}: result={Result}; no priority group assigned, leaving enrollment Unverified.",
                icn, result.Result);
        }

        // If the determination is a hard No, mark enrollment Rejected so the
        // patient does not appear as enrollable downstream.
        if (result.Result == EligibilityDeterminationResult.NotEligible
            || result.Result == EligibilityDeterminationResult.NotEligibleIncome)
        {
            await workflow.SetEnrollmentStatusAsync(
                EnrollmentStatus.Rejected,
                changedByUserId: "REGISTRATION",
                notes: $"Auto-determination at registration: {result.Result}");
        }
    }
}
