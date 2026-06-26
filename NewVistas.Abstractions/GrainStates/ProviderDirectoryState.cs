// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight staff/provider summary held in the singleton ProviderDirectoryGrain
/// for name lookup. Denormalized from NEW PERSON (File #200) so a clinician can find
/// a colleague by name without enumerating every USER:{id} grain.
/// </summary>
[GenerateSerializer]
public record ProviderDirectoryEntry
{
    /// <summary>
    /// The provider's user id (ASP.NET Identity id, no "USER:" prefix). This is the
    /// same value used as the providerId everywhere — care-team keys, order
    /// authorship, PROV-PAT-IDX panels — so a picked entry slots in directly.
    /// </summary>
    [Id(0)]
    public string UserId { get; init; } = string.Empty;

    /// <summary>Display name, VistA format LAST,FIRST MI (e.g. "SMITH,JOHN A").</summary>
    [Id(1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Position title (e.g. "Staff Physician", "Registered Nurse"). Optional.</summary>
    [Id(2)]
    public string? Title { get; init; }

    /// <summary>Provider type (PHYSICIAN, NURSE PRACTITIONER, NURSE, PHARMACIST, …). Optional.</summary>
    [Id(3)]
    public string? ProviderType { get; init; }

    /// <summary>Clinical specialty (e.g. "Internal Medicine", "Cardiology"). Optional.</summary>
    [Id(4)]
    public string? Specialty { get; init; }

    /// <summary>Service/section (e.g. "MEDICINE", "SURGERY"). Optional.</summary>
    [Id(5)]
    public string? ServiceSection { get; init; }

    /// <summary>False for terminated/disabled staff — excluded from search results.</summary>
    [Id(6)]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Singleton state for the ProviderDirectoryGrain (key: "PROVIDER-DIRECTORY").
/// A cross-reference of all staff/providers keyed by UserId for O(1) upsert and
/// O(n) name search — n is facility staff count (hundreds–thousands), trivially small.
/// Maintained by NewPersonGrain on every profile/active-status change.
/// </summary>
[GenerateSerializer]
public class ProviderDirectoryState
{
    /// <summary>All provider entries keyed by UserId (ASP.NET Identity id).</summary>
    [Id(0)]
    public Dictionary<string, ProviderDirectoryEntry> Providers { get; set; } = new();
}
