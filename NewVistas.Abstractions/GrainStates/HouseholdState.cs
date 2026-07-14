// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Structural position of a member within a household (distinct from the free-text relationship).</summary>
[GenerateSerializer]
public enum HouseholdMemberRole
{
    Member = 0,
    HeadOfHousehold = 1,
    Spouse = 2,
    Dependent = 3,
    Child = 4,
    Other = 5
}

/// <summary>The household's housing situation (residence type).</summary>
[GenerateSerializer]
public enum HouseholdHousingType
{
    Unknown = 0,
    Owned = 1,
    Rented = 2,
    WithFamilyOrFriends = 3,
    TransitionalHousing = 4,
    Shelter = 5,
    Homeless = 6,
    AssistedLiving = 7,
    Other = 8
}

/// <summary>
/// One member of a household. Anchored on a <b>Person</b> (ADR-002 <c>PERSON:{guid}</c>), NOT a
/// patient — so a member who is not (yet) a patient, or who is also a staff member / a relative on
/// another chart, resolves to the same human. Membership is time-bounded (<see cref="LeftDate"/>):
/// people move between households, and the household outlives any one member.
/// </summary>
[GenerateSerializer]
public record HouseholdMember
{
    /// <summary>The Person id (<c>PERSON:{guid}</c>) this member is.</summary>
    [Id(0)] public string PersonId { get; set; } = string.Empty;
    /// <summary>Denormalized display name at add time (cheap reads).</summary>
    [Id(1)] public string DisplayName { get; set; } = string.Empty;
    /// <summary>Free-text relationship (e.g. "Spouse", "Son", "Grandmother") — mirrors PersonRelativeAppearance.</summary>
    [Id(2)] public string Relationship { get; set; } = string.Empty;
    /// <summary>Structural role in the household.</summary>
    [Id(3)] public HouseholdMemberRole Role { get; set; }
    [Id(4)] public DateTime JoinedDate { get; set; }
    /// <summary>Set when the member leaves (moves out) — the membership is retained for history.</summary>
    [Id(5)] public DateTime? LeftDate { get; set; }
}

/// <summary>An audit line in a household's history.</summary>
[GenerateSerializer]
public record HouseholdChangeLogEntry
{
    [Id(0)] public DateTime Timestamp { get; set; }
    [Id(1)] public string User { get; set; } = string.Empty;
    [Id(2)] public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// A household — a family/residential unit of PEOPLE that outlives any one member. Grain key:
/// <c>HOUSEHOLD:{guid}</c>. NewVistas is otherwise patient-as-island; this is the general social
/// household the whole-person / community-health model needs. It is DISTINCT from
/// <c>IncomeHouseholdGrain</c> (the financial means-test household, patient-anchored) — the two are
/// complementary and may be cross-referenced later.
/// </summary>
[GenerateSerializer]
public class HouseholdState
{
    [Id(0)] public string HouseholdId { get; set; } = string.Empty;
    /// <summary>Display label (e.g. "Smith Household").</summary>
    [Id(1)] public string Label { get; set; } = string.Empty;
    /// <summary>Person id of the head of household (may be empty until set).</summary>
    [Id(2)] public string HeadOfHouseholdPersonId { get; set; } = string.Empty;
    /// <summary>Members, including those who have left (LeftDate set).</summary>
    [Id(3)] public List<HouseholdMember> Members { get; set; } = new();

    [Id(4)] public HouseholdHousingType HousingType { get; set; }
    [Id(5)] public string? StreetAddress { get; set; }
    [Id(6)] public string? City { get; set; }
    [Id(7)] public string? State { get; set; }
    [Id(8)] public string? ZipCode { get; set; }

    [Id(9)] public List<HouseholdChangeLogEntry> ChangeLog { get; set; } = new();
    [Id(10)] public DateTime CreatedDate { get; set; }
    [Id(11)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>One person→household membership link (current or historical).</summary>
[GenerateSerializer]
public record PersonHouseholdLink
{
    [Id(0)] public string HouseholdId { get; set; } = string.Empty;
    [Id(1)] public DateTime JoinedDate { get; set; }
    [Id(2)] public DateTime? LeftDate { get; set; }
}

/// <summary>
/// Reverse index: which household(s) a Person belongs to (current + historical). Grain key:
/// <c>PERSON-HOUSEHOLD-IDX:{personId}</c>. The patient→Person→household resolution path.
/// </summary>
[GenerateSerializer]
public class PersonHouseholdIndexState
{
    [Id(0)] public string PersonId { get; set; } = string.Empty;
    [Id(1)] public List<PersonHouseholdLink> Links { get; set; } = new();
}
