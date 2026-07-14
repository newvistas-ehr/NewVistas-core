// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// The community-resource directory (grain key <c>RESOURCE-DIRECTORY</c>) — a searchable catalog of
/// agencies/services that Social Work / SDOH referrals point at. Reads are open; edits are gated at
/// the surface by the SOCIAL_CARE feature.
/// </summary>
public interface IResourceDirectoryGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates a resource (generates an id when empty). Returns the resource id.</summary>
    Task<string> AddOrUpdateAsync(CommunityResource resource);

    Task RemoveAsync(string resourceId);

    Task<CommunityResource?> GetAsync(string resourceId);

    /// <summary>All active resources.</summary>
    Task<List<CommunityResource>> GetAllAsync();

    /// <summary>
    /// Active resources filtered by service type (optional) and/or a free-text term matched against
    /// name / description / service area (optional). Both null returns all active resources.
    /// </summary>
    Task<List<CommunityResource>> SearchAsync(SocialWorkReferralServiceType? serviceType, string? text);
}
