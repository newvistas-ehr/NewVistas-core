// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A home-health agency in the facility's agency directory — the delivering organization an
/// externally-delivered (<see cref="HomeCareDeliveryModel.ExternalAgency"/>) home-care episode points
/// at, and the picker a coordinator chooses from when referring a patient out. Mirrors the
/// PharmacyDirectory shape: an in-house entry (the health system's own licensed agency) is the
/// delivering org for hospital-provided care that bills through an owned agency, exactly as the
/// INPATIENT pharmacy is the hospital's own dispensing site.
/// </summary>
[GenerateSerializer]
public record HomeHealthAgencyEntry
{
    /// <summary>Internal directory id / grain reference (e.g. "HHA-VALLEY-VNA").</summary>
    [Id(0)] public string AgencyId { get; init; } = string.Empty;

    /// <summary>Display name, e.g. "VALLEY VNA HOME HEALTH".</summary>
    [Id(1)] public string Name { get; init; } = string.Empty;

    /// <summary>
    /// IN_HOUSE (the health system's own agency — the hospital-provided delivering org) or EXTERNAL
    /// (an independent agency we refer out to and coordinate).
    /// </summary>
    [Id(2)] public string Kind { get; init; } = HomeHealthAgencyKinds.External;

    /// <summary>National Provider Identifier (10-digit), if known.</summary>
    [Id(3)] public string? Npi { get; init; }

    /// <summary>CMS Certification Number — the agency's Medicare provider number.</summary>
    [Id(4)] public string? Ccn { get; init; }

    [Id(5)] public string? Address { get; init; }
    [Id(6)] public string? City { get; init; }
    [Id(7)] public string? State { get; init; }
    [Id(8)] public string? Zip { get; init; }
    [Id(9)] public string? Phone { get; init; }
    [Id(10)] public string? Fax { get; init; }

    /// <summary>Free-text service area (e.g. "Hampden &amp; Hampshire County, MA").</summary>
    [Id(11)] public string? ServiceArea { get; init; }

    /// <summary>Disciplines the agency staffs (reuses the home-care discipline vocabulary).</summary>
    [Id(12)] public List<HomeCareDiscipline> Disciplines { get; init; } = new();

    /// <summary>True if the agency currently accepts new referrals.</summary>
    [Id(13)] public bool AcceptsReferrals { get; init; } = true;

    /// <summary>False for closed/inactive agencies — excluded from search.</summary>
    [Id(14)] public bool IsActive { get; init; } = true;
}

/// <summary>Well-known <see cref="HomeHealthAgencyEntry.Kind"/> values.</summary>
public static class HomeHealthAgencyKinds
{
    /// <summary>The health system's own licensed home-health agency (delivers hospital-provided care).</summary>
    public const string InHouse = "IN_HOUSE";
    /// <summary>An independent home-health agency we refer out to.</summary>
    public const string External = "EXTERNAL";
}

/// <summary>
/// Singleton state for the <c>HomeHealthAgencyDirectoryGrain</c> (key "HHA-DIRECTORY") — a
/// facility-wide directory of home-health agencies keyed by
/// <see cref="HomeHealthAgencyEntry.AgencyId"/>. Mirrors the PharmacyDirectory singleton pattern.
/// </summary>
[GenerateSerializer]
public class HomeHealthAgencyDirectoryState
{
    /// <summary>All agencies keyed by AgencyId.</summary>
    [Id(0)] public Dictionary<string, HomeHealthAgencyEntry> Agencies { get; set; } = new();

    /// <summary>
    /// True once the demo agency set has been auto-seeded. Guarding on this (rather than a "dictionary
    /// empty" check) keeps the seed deterministic even if an agency was added before the first read.
    /// </summary>
    [Id(1)] public bool DemoSeeded { get; set; }
}
