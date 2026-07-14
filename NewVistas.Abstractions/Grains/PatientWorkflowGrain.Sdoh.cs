// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Coded SDOH screening + the closed loop (Whole-Person Social Care, Phase 2). A screening's positive
/// domains are computed by <see cref="SdohScreeningCatalog"/>; the clinician then confirms each
/// intervention — a Z-code onto the problem list (via the existing <c>AddProblemAsync</c>) and a
/// referral into the existing Social Work referral machinery (<c>CreateSocialWorkReferralAsync</c>).
/// Nothing auto-fires. Feature-gated by <c>SOCIAL_CARE</c>.
/// </summary>
public partial class PatientWorkflowGrain
{
    private ISdohScreeningIndexGrain SdohIndex() => GrainFactory.GetGrain<ISdohScreeningIndexGrain>($"SDOH-IDX:{PatientId}");

    public async Task<string> RecordSdohScreeningAsync(string instrumentName, List<SdohScreeningResponse> responses, string byUser)
    {
        await RequireSocialCareFeatureAsync();

        string screeningId = $"SDOH:{Guid.NewGuid()}";
        ISdohScreeningGrain grain = GrainFactory.GetGrain<ISdohScreeningGrain>(screeningId);
        await grain.RecordScreeningAsync(PatientId, instrumentName, responses ?? new(), byUser);
        SdohScreeningState state = await grain.GetAsync();

        await SdohIndex().AddEntryAsync(new SdohScreeningSummary
        {
            ScreeningId = screeningId,
            InstrumentName = state.InstrumentName,
            ScreeningDate = state.ScreeningDate,
            PositiveDomainCount = state.Findings.Count
        });

        // Fan out to the per-domain cohort shards for reporting.
        foreach (SdohFinding f in state.Findings)
            await GrainFactory.GetGrain<ISdohCohortIndexGrain>($"SDOH-COHORT:{f.Domain}").AddPatientAsync(PatientId);

        return screeningId;
    }

    public async Task<List<SdohScreeningSummary>> GetSdohScreeningsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SocialCareFeature);
        if (!enabled)
            return new();
        return await SdohIndex().GetAllAsync();
    }

    public Task<SdohScreeningState> GetSdohScreeningAsync(string screeningId) =>
        GrainFactory.GetGrain<ISdohScreeningGrain>(screeningId).GetAsync();

    /// <summary>Closes the loop: adds the domain's mapped Z-code to the problem list, citing the screening.</summary>
    public async Task<string> AddSdohProblemAsync(string screeningId, SdohDomain domain, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        SdohFinding f = SdohScreeningCatalog.FindingFor(domain);
        string comment = $"Social determinant of health — {f.Display} identified on SDOH screening ({screeningId}).";

        string problemId = await AddProblemAsync(
            diagnosis: f.ZCodeDisplay, diagnosisCode: f.ZCode, condition: null, priority: null,
            dateOfOnset: null, providerId: null, providerName: null, clinicId: null, clinicName: null,
            isServiceConnected: false, comments: comment);

        await GrainFactory.GetGrain<ISdohScreeningGrain>(screeningId)
            .RecordActionAsync(domain, SdohActionType.ProblemAdded, problemId, byUser);
        return problemId;
    }

    /// <summary>Closes the loop: opens a Social Work referral to the domain's matching service type.</summary>
    public async Task<string> CreateSdohReferralAsync(string screeningId, SdohDomain domain, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        SdohFinding f = SdohScreeningCatalog.FindingFor(domain);

        string referralId = await CreateSocialWorkReferralAsync(
            referralDate: DateTime.UtcNow,
            referralSource: "SDOH screening",
            referralReason: $"{f.Display} — identified on SDOH screening",
            serviceType: f.ReferralServiceType,
            agencyName: null, agencyContact: null, agencyPhone: null,
            socialWorkerId: null, socialWorkerName: byUser,
            followUpDate: null, assessmentId: null, locationId: null, locationName: null,
            comments: $"Auto-suggested from SDOH screening {screeningId}.");

        await GrainFactory.GetGrain<ISdohScreeningGrain>(screeningId)
            .RecordActionAsync(domain, SdohActionType.ReferralCreated, referralId, byUser);
        return referralId;
    }
}
