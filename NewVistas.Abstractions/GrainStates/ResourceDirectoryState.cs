// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A community resource / agency the site can refer patients to — the thing a Social Work / SDOH
/// referral points AT (today those referrals carry free-text agency fields with nothing to pick from).
/// The service type reuses <see cref="SocialWorkReferralServiceType"/> so a positive social need maps
/// straight to the matching resources; <see cref="TaxonomyCode"/> preserves an AIRS / 2-1-1
/// human-services taxonomy classification when available.
/// </summary>
[GenerateSerializer]
public record CommunityResource
{
    [Id(0)] public string ResourceId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public SocialWorkReferralServiceType ServiceType { get; set; }
    /// <summary>AIRS / 2-1-1 human-services taxonomy code (optional).</summary>
    [Id(3)] public string? TaxonomyCode { get; set; }
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public string? Phone { get; set; }
    [Id(6)] public string? Website { get; set; }
    [Id(7)] public string? Address { get; set; }
    [Id(8)] public string? City { get; set; }
    [Id(9)] public string? State { get; set; }
    [Id(10)] public string? Zip { get; set; }
    /// <summary>Free-text service area (e.g. "Essex County", "statewide").</summary>
    [Id(11)] public string? ServiceArea { get; set; }
    [Id(12)] public string? Eligibility { get; set; }
    [Id(13)] public bool IsActive { get; set; } = true;
}

/// <summary>Directory of community resources. Grain key: <c>RESOURCE-DIRECTORY</c>.</summary>
[GenerateSerializer]
public class ResourceDirectoryState
{
    [Id(0)] public List<CommunityResource> Resources { get; set; } = new();
}
