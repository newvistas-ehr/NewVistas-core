// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Case-management goal/outcome spine (Whole-Person Social Care roadmap R2). Thin flag-gated entry
/// points; goal-detail operations (work-steps, follow-ups, outcome) go straight to the case grain.
/// </summary>
public partial class PatientWorkflowGrain
{
    private ICaseManagementGrain CaseManagement() =>
        GrainFactory.GetGrain<ICaseManagementGrain>($"CASE-MGMT:{PatientId}");

    /// <summary>The patient's case-management record. Open read; empty when SOCIAL_CARE is off.</summary>
    public async Task<CaseManagementState> GetCaseManagementAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SocialCareFeature);
        if (!enabled)
            return new CaseManagementState { PatientId = PatientId };
        return await CaseManagement().GetAsync();
    }

    /// <summary>Opens a case-management goal for this patient (optionally citing an SDOH domain / referral).</summary>
    public async Task<string> AddCaseGoalAsync(string description, CaseGoalDomain domain, DateTime? targetDate, string? sourceReference, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        return await CaseManagement().AddGoalAsync(description, domain, targetDate, sourceReference, byUser);
    }
}
