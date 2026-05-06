// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// External Referral Tracking — Site Flavor Architecture (Option 4: Composition).
/// Checks the EXTERNAL_REFERRAL_TRACKING feature flag before delegating to
/// the optional IExternalReferralGrain. If the feature is not enabled,
/// the referral grains are never activated and consume no resources.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string ExternalReferralFeature = "EXTERNAL_REFERRAL_TRACKING";

    public async Task<ExternalReferralState> CreateExternalReferralAsync(
        string referralType, string externalFacilityName, string? externalFacilityId,
        string? externalProviderName, string? externalProviderId,
        string purpose, string? diagnosis, string urgency,
        string referredByProviderId, string referredByProviderName,
        string? consultId, string? authorizationNumber,
        DateTime? appointmentDateTime, string? specialInstructions)
    {
        // ── Feature gate ────────────────────────────────────────────
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "External referral tracking is not enabled for this site. Enable the EXTERNAL_REFERRAL_TRACKING feature in Site Parameters.");

        // Get patient name for the referral record
        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        IExternalReferralGrain referralGrain =
            GrainFactory.GetGrain<IExternalReferralGrain>(referralId);

        ExternalReferralState result = await referralGrain.CreateReferralAsync(
            PatientId, patient.Name, referralType,
            externalFacilityName, externalFacilityId,
            externalProviderName, externalProviderId,
            purpose, diagnosis, urgency,
            referredByProviderId, referredByProviderName,
            consultId, authorizationNumber,
            appointmentDateTime, specialInstructions);

        await LogAuditEventAsync(
            "REFERRAL", "CREATE_EXTERNAL_REFERRAL", "ExternalReferral", referralId,
            referredByProviderId, referredByProviderName, null, null,
            $"External referral to {externalFacilityName} for {purpose}");

        return result;
    }

    public async Task<List<ExternalReferralIndexEntry>> GetExternalReferralsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled) return [];

        IExternalReferralIndexGrain index =
            GrainFactory.GetGrain<IExternalReferralIndexGrain>("EXT-REF-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<ExternalReferralState> GetExternalReferralAsync(string referralId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException("External referral tracking is not enabled for this site.");

        IExternalReferralGrain grain = GrainFactory.GetGrain<IExternalReferralGrain>(referralId);
        return await grain.GetReferralAsync();
    }

    public async Task UpdateExternalReferralStatusAsync(
        string referralId, string status, string? statusReason,
        string updatedById, string updatedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException("External referral tracking is not enabled for this site.");

        IExternalReferralGrain grain = GrainFactory.GetGrain<IExternalReferralGrain>(referralId);
        await grain.UpdateStatusAsync(status, statusReason, updatedById, updatedByName);
    }

    public async Task CompleteExternalReferralAsync(
        string referralId, DateTime completionDate,
        string? outcomeNotes, string? clinicalFindings)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException("External referral tracking is not enabled for this site.");

        IExternalReferralGrain grain = GrainFactory.GetGrain<IExternalReferralGrain>(referralId);
        await grain.RecordCompletionAsync(completionDate, outcomeNotes, clinicalFindings);
    }

    // ─── Contract Health Services (CHS / PRC) ─────────────────────────────
    // Three-step authorization workflow per 25 CFR Part 136. Each is auth-gated
    // by [RequiresSecurityKey(CanAuthorizeChs)] on the interface; the
    // AuditCallFilter records the action automatically via [AuditAction].

    public async Task RequestChsAuthorizationAsync(
        string referralId, decimal estimatedCost, string medicalPriorityClass,
        bool alternateResourcesChecked, string? alternateResourcesNote,
        string requestedByProviderId, string requestedByProviderName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException("External referral tracking is not enabled for this site.");

        IExternalReferralGrain grain = GrainFactory.GetGrain<IExternalReferralGrain>(referralId);
        await grain.RequestChsAuthorizationAsync(
            estimatedCost, medicalPriorityClass, alternateResourcesChecked,
            alternateResourcesNote, requestedByProviderId, requestedByProviderName);
    }

    public async Task ApproveChsAuthorizationAsync(
        string referralId, decimal authorizedAmount, string? authorizationNumber,
        string approvedById, string approvedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException("External referral tracking is not enabled for this site.");

        // Eligibility check: the patient must hold the IHS CHS code stamped by
        // IhsTribalEligibilityPolicy at registration. Without this guard, a
        // direct-care-only patient could be approved for CHS funding.
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        const string ihsChsCode = "IHS CHS";
        if (!string.Equals(patient.PrimaryEligibilityCode, ihsChsCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Patient {PatientId} does not hold CHS eligibility ('{patient.PrimaryEligibilityCode ?? "(none)"}' on file; expected '{ihsChsCode}'). " +
                "Re-run eligibility determination or deny this request.");

        IExternalReferralGrain grain = GrainFactory.GetGrain<IExternalReferralGrain>(referralId);
        await grain.ApproveChsAuthorizationAsync(
            authorizedAmount, authorizationNumber, approvedById, approvedByName);
    }

    public async Task DenyChsAuthorizationAsync(
        string referralId, string denialReason, string deniedById, string deniedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(ExternalReferralFeature);
        if (!enabled)
            throw new InvalidOperationException("External referral tracking is not enabled for this site.");

        IExternalReferralGrain grain = GrainFactory.GetGrain<IExternalReferralGrain>(referralId);
        await grain.DenyChsAuthorizationAsync(denialReason, deniedById, deniedByName);
    }
}
