// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A household — a Person-anchored family/residential unit that outlives any one member. Grain key:
/// <c>HOUSEHOLD:{guid}</c>. Maintains the <see cref="IPersonHouseholdIndexGrain"/> reverse index as
/// members join and leave. Reads are open; mutations are gated at the workflow façade by the
/// <c>SOCIAL_CARE</c> feature.
/// </summary>
public interface IHouseholdGrain : IGrainWithStringKey
{
    /// <summary>Creates the household with a label. Idempotent-ish: re-create updates the label only.</summary>
    Task CreateAsync(string label, string byUser);

    /// <summary>
    /// Adds a member (by Person id). Idempotent on an active membership. If the member's role is
    /// HeadOfHousehold — or this is the first member — they become head. Updates the person index.
    /// </summary>
    Task AddMemberAsync(string personId, string displayName, string relationship, HouseholdMemberRole role, string byUser);

    /// <summary>Marks a member as having left (sets LeftDate; membership retained for history). Updates the index.</summary>
    Task RemoveMemberAsync(string personId, string byUser);

    /// <summary>Sets the head of household (the person must be an active member).</summary>
    Task SetHeadAsync(string personId, string byUser);

    /// <summary>Sets the household's housing situation and address.</summary>
    Task SetHousingAsync(HouseholdHousingType housingType, string? street, string? city, string? state, string? zip, string byUser);

    Task<HouseholdState> GetAsync();
}

/// <summary>
/// Reverse index of a Person's household memberships (current + historical). Grain key:
/// <c>PERSON-HOUSEHOLD-IDX:{personId}</c>. Maintained by <see cref="IHouseholdGrain"/>.
/// </summary>
public interface IPersonHouseholdIndexGrain : IGrainWithStringKey
{
    /// <summary>Records the person joining a household (idempotent on an open link).</summary>
    Task AddLinkAsync(string householdId, DateTime joinedDate);

    /// <summary>Closes the person's open link to a household (sets LeftDate).</summary>
    Task CloseLinkAsync(string householdId, DateTime leftDate);

    /// <summary>The person's current household id (open link), or null if none.</summary>
    Task<string?> GetCurrentHouseholdIdAsync();

    Task<PersonHouseholdIndexState> GetAsync();
}
