// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for external (community care) referral tracking.
/// Enabled per site via ISiteParametersGrain.Features containing "EXTERNAL_REFERRAL_TRACKING".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to IHS RPMS Referred Care Information System (RCIS) and
/// VA Community Care referral tracking.
/// Keyed by referral ID (e.g., "EXT-REF:{guid}").
/// </summary>
public interface IExternalReferralGrain : IGrainWithStringKey
{
    Task<ExternalReferralState> GetReferralAsync();

    Task<ExternalReferralState> CreateReferralAsync(
        string patientId,
        string patientName,
        string referralType,
        string externalFacilityName,
        string? externalFacilityId,
        string? externalProviderName,
        string? externalProviderId,
        string purpose,
        string? diagnosis,
        string urgency,
        string referredByProviderId,
        string referredByProviderName,
        string? consultId,
        string? authorizationNumber,
        DateTime? appointmentDateTime,
        string? specialInstructions);

    Task UpdateStatusAsync(string status, string? statusReason, string updatedById, string updatedByName);
    Task RecordAppointmentAsync(DateTime appointmentDateTime, string? confirmationNumber);
    Task RecordCompletionAsync(DateTime completionDate, string? outcomeNotes, string? clinicalFindings);
    Task RecordDenialAsync(string denialReason, string deniedById, string deniedByName);
    Task AddFollowUpAsync(string followUpNote, string authorName);
    Task AttachDocumentAsync(string documentId, string documentType, string description);

    // ─── Contract Health Services (CHS / PRC) ─────────────────────────────
    // IHS-specific authorization workflow per 25 CFR Part 136. Used only when
    // the referral is funded through the tribe's Contract Health program.
    // Eligibility (patient holds the IHS CHS code from
    // IhsTribalEligibilityPolicy) is verified by the workflow grain at
    // approval time, not here.

    /// <summary>
    /// Mark this referral as a CHS-funded request and record the cost
    /// estimate, IHS Medical Priority Class, and the requesting provider's
    /// confirmation that alternate resources have been considered. Sets
    /// status to <c>"PENDING_CHS_AUTH"</c>; the CHS coordinator approves or
    /// denies via the next two methods.
    /// </summary>
    Task RequestChsAuthorizationAsync(
        decimal estimatedCost,
        string medicalPriorityClass,
        bool alternateResourcesChecked,
        string? alternateResourcesNote,
        string requestedByProviderId,
        string requestedByProviderName);

    /// <summary>
    /// CHS coordinator approves the authorization. Records the authorized
    /// dollar amount and (optional) external authorization number; sets
    /// status to <c>"AUTHORIZED"</c>. Caller is responsible for verifying
    /// patient CHS eligibility before invoking.
    /// </summary>
    Task ApproveChsAuthorizationAsync(
        decimal authorizedAmount,
        string? authorizationNumber,
        string approvedById,
        string approvedByName);

    /// <summary>
    /// CHS coordinator denies the authorization. Sets status to
    /// <c>"DENIED"</c> with the supplied reason. Common reasons: ineligible
    /// patient, fund pool exhausted, alternate resources available, priority
    /// class deferred for the current fiscal year.
    /// </summary>
    Task DenyChsAuthorizationAsync(
        string denialReason,
        string deniedById,
        string deniedByName);
}

/// <summary>
/// System-level index grain for external referrals.
/// Singleton keyed by "EXT-REF-IDX".
/// Supports queries by patient, facility, status, and date range.
/// </summary>
public interface IExternalReferralIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(ExternalReferralIndexEntry entry);
    Task<List<ExternalReferralIndexEntry>> GetByPatientAsync(string patientId);
    Task<List<ExternalReferralIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<List<ExternalReferralIndexEntry>> GetByFacilityAsync(string facilityName, int maxResults = 50);
    Task<List<ExternalReferralIndexEntry>> GetPendingFollowUpsAsync(int maxResults = 50);
    Task<List<ExternalReferralIndexEntry>> SearchAsync(string? patientId, string? status, string? facility, int maxResults = 50);
    Task<int> GetCountAsync();
}
