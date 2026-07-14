// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Refer a patient to a community-resource-directory entry (Whole-Person Social Care roadmap R3).
/// Populates the existing Social Work referral from the chosen directory resource — the referral now
/// points at a real agency instead of a free-text blank.
/// </summary>
public partial class PatientWorkflowGrain
{
    public async Task<string> ReferToResourceAsync(string resourceId, string reason, string byUser)
    {
        await RequireSocialCareFeatureAsync();
        CommunityResource? r = await GrainFactory.GetGrain<IResourceDirectoryGrain>("RESOURCE-DIRECTORY").GetAsync(resourceId);
        if (r is null)
            throw new InvalidOperationException("Resource not found in the directory.");

        return await CreateSocialWorkReferralAsync(
            referralDate: DateTime.UtcNow,
            referralSource: "Resource directory",
            referralReason: string.IsNullOrWhiteSpace(reason) ? $"Referral to {r.Name}" : reason,
            serviceType: r.ServiceType,
            agencyName: r.Name,
            agencyContact: r.Website,
            agencyPhone: r.Phone,
            socialWorkerId: null, socialWorkerName: byUser,
            followUpDate: null, assessmentId: null, locationId: null, locationName: null,
            comments: $"Referred to {r.Name} (community resource {resourceId}).");
    }
}
