// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;

namespace NewVistas.Abstractions.Eligibility;

/// <summary>
/// Per-deployment gate that decides whether and how to determine a newly-registered
/// patient's healthcare eligibility. Different organizations have different rules:
///
///   • <b>VA-aligned deployments</b> apply 38 CFR §17.36 — priority groups 1–8,
///     means-test, service-connected disability, POW/Purple Heart, VA pension —
///     via <see cref="IAutoEligibilityDeterminationGrain"/> and update the
///     patient's enrollment record on the spot.
///   • <b>IHS / international / private deployments</b> have entirely different
///     rules (tribal membership, cash-pay, NHS/insurance-driven, etc.) and may
///     defer eligibility to a later workflow.
///   • <b>Dev / test deployments</b> typically want no determination at all.
///
/// One implementation is registered as a singleton per silo. <see cref="PatientRegistrationGrain"/>
/// invokes it after the patient grain, MPI correlation, and search-index entry
/// are in place. The policy may apply enrollment changes via the supplied
/// <see cref="IPatientWorkflowGrain"/> or take no action.
///
/// The default registration in <c>CommonSiloConfig.AddCommonSiloServices</c> is
/// <see cref="NoOpRegistrationEligibilityPolicy"/>; VA-aligned site profiles
/// override it with <see cref="VaRegistrationEligibilityPolicy"/>.
/// </summary>
public interface IRegistrationEligibilityPolicy
{
    /// <summary>
    /// Determine and (optionally) apply healthcare eligibility for a newly-registered
    /// patient. Implementations must be idempotent — registration may be retried.
    /// </summary>
    /// <param name="icn">The patient's newly-issued ICN (also the workflow grain key).</param>
    /// <param name="request">The originating registration request, including any policy-specific hint fields.</param>
    /// <param name="workflow">The patient's workflow grain, ready for enrollment-related calls.</param>
    Task DetermineAndApplyAsync(
        string icn,
        RegistrationRequest request,
        IPatientWorkflowGrain workflow);
}
