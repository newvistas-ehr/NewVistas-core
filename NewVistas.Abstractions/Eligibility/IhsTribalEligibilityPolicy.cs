// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Eligibility;

/// <summary>
/// IHS / tribal eligibility policy. Applies 38 CFR Part 136 (IHS Beneficiary
/// Eligibility) rules to a newly-registered patient by reading the tribal hint
/// fields on the <see cref="RegistrationRequest"/> and stamping the resulting
/// eligibility code + enrollment record on the patient.
///
/// Determination tiers:
///   • <b>IHS DIRECT</b> — eligible for direct care at the tribal facility.
///     Granted to: enrolled tribal members, patients with an
///     <see cref="RegistrationRequest.IhsEligibleByCategory"/> code (e.g. a
///     non-Indian woman pregnant by an eligible Indian).
///   • <b>IHS CHS</b> — eligible for Contract Health Services in addition to
///     direct care. Granted to direct-care-eligible patients who reside in the
///     Contract Health Service Delivery Area (CHSDA) and have ≥180 days of
///     CHSDA residency. CHS access requires further per-encounter
///     authorization (handled separately by the CHS workflow).
///   • <b>(no change)</b> — when no tribal hints are provided and no
///     IhsEligibleByCategory is set, the policy short-circuits. The patient is
///     still registered (basic demographics + ICN) but no enrollment record is
///     created. Suitable for non-IHS-eligible patients seen in the clinic
///     (e.g., self-pay / private-insurance walk-ins at a tribal facility).
///
/// Behaviour mirrors <see cref="VaRegistrationEligibilityPolicy"/>: short-circuit
/// when no eligibility hint is supplied, otherwise stamp the patient grain and
/// create an enrollment record. Each tribe maps roles to the
/// <c>CanRegisterPatients</c> security key independently of this policy.
/// </summary>
public sealed class IhsTribalEligibilityPolicy : IRegistrationEligibilityPolicy
{
    /// <summary>Eligibility code stamped on PatientState for direct-care-only patients.</summary>
    public const string DirectCareCode = "IHS DIRECT";

    /// <summary>Eligibility code stamped on PatientState for CHS-eligible patients.</summary>
    public const string ChsEligibleCode = "IHS CHS";

    /// <summary>
    /// Minimum CHSDA residency in days required for CHS eligibility per
    /// 25 CFR § 136.23 (180 days is the long-standing IHS rule).
    /// </summary>
    public const int MinChsResidencyDays = 180;

    /// <summary>
    /// Priority-group string written on the enrollment record for direct-care
    /// patients. Free-text per the VA-style enrollment grain; tribal sites use
    /// these IHS-flavoured tokens instead of the VA's "1"–"8" priority groups.
    /// </summary>
    public const string DirectCarePriorityGroup = "IHS-DIRECT";

    /// <summary>Priority-group string for CHS-eligible patients.</summary>
    public const string ChsPriorityGroup = "IHS-CHS";

    private readonly ILogger<IhsTribalEligibilityPolicy> _logger;

    public IhsTribalEligibilityPolicy(ILogger<IhsTribalEligibilityPolicy> logger)
    {
        _logger = logger;
    }

    public async Task DetermineAndApplyAsync(
        string icn,
        RegistrationRequest request,
        IPatientWorkflowGrain workflow)
    {
        bool isTribalMember = request.IsTribalMember == true;
        bool hasCategory = !string.IsNullOrWhiteSpace(request.IhsEligibleByCategory);

        if (!isTribalMember && !hasCategory)
        {
            _logger.LogDebug(
                "IHS eligibility policy skipped for ICN {Icn}: no tribal hint supplied.",
                icn);
            return;
        }

        // Direct care is granted by either tribal membership or category eligibility.
        bool directCareEligible = isTribalMember || hasCategory;

        // CHS adds the residency requirements on top of direct-care eligibility.
        bool chsEligible =
            directCareEligible
            && request.ResidesInChsda == true
            && (request.ChsdaResidencyDays ?? 0) >= MinChsResidencyDays;

        string eligibilityCode = chsEligible ? ChsEligibleCode : DirectCareCode;
        string priorityGroup = chsEligible ? ChsPriorityGroup : DirectCarePriorityGroup;

        // Stamp the patient state with eligibility info. IHS patients are not
        // veterans (the VA Veteran="Y" flag stays "N" unless a separate VA
        // policy also runs — typical site profiles register only one policy).
        await workflow.UpdateVeteranInfoAsync(
            veteran: "N",
            serviceConnectedPercentage: null,
            eligibilityCode: eligibilityCode,
            primaryEligibilityCode: eligibilityCode);

        // Create the enrollment record. IHS direct care has no copay obligation
        // for eligible Indians (25 CFR § 136.14 and tribal sovereignty); CHS
        // access is policy-managed per encounter, not at registration.
        await workflow.SetEnrollmentStatusAsync(
            EnrollmentStatus.Verified,
            changedByUserId: "REGISTRATION",
            notes: BuildEnrollmentNote(request, chsEligible));

        await workflow.SetEnrollmentPriorityGroupAsync(
            priorityGroup: priorityGroup,
            prioritySubgroup: request.TribalAffiliation,  // record the tribe in the subgroup slot
            meansTestRequired: false,
            copayExempt: true,
            copayExemptionReason: "IHS_BENEFICIARY");

        _logger.LogInformation(
            "IHS eligibility for ICN {Icn}: code={Code}, priorityGroup={Group}, tribe={Tribe}.",
            icn, eligibilityCode, priorityGroup, request.TribalAffiliation ?? "(unspecified)");
    }

    private static string BuildEnrollmentNote(RegistrationRequest request, bool chsEligible)
    {
        if (request.IsTribalMember == true)
        {
            string tribe = string.IsNullOrWhiteSpace(request.TribalAffiliation)
                ? "(tribal affiliation not recorded)"
                : request.TribalAffiliation;
            string suffix = chsEligible
                ? $" CHSDA residency: {request.ChsdaResidencyDays} days."
                : string.Empty;
            return $"Auto-determination at registration: tribal member of {tribe}.{suffix}";
        }

        return $"Auto-determination at registration: eligible by category '{request.IhsEligibleByCategory}'.";
    }
}
