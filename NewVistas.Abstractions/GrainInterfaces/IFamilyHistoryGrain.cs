// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A patient's structured family history — one entry per relative (relationship, conditions with age
/// at diagnosis, vital status). Feeds the hereditary-risk red-flag assessment. Key pattern: the patient id.
/// </summary>
public interface IFamilyHistoryGrain : IGrainWithStringKey
{
    /// <summary>Adds a family member entry; returns the member id.</summary>
    Task<string> AddMemberAsync(FamilyMemberHistoryEntry member);

    /// <summary>Adds a condition to an existing family member.</summary>
    Task AddConditionAsync(string memberId, FamilyConditionEntry condition);

    /// <summary>Links (or clears, when null/empty) a family member to a Person anchor (ADR-002).</summary>
    Task SetMemberPersonLinkAsync(string memberId, string? personId);

    Task RemoveMemberAsync(string memberId);

    Task<FamilyHistoryState> GetAsync();
}
