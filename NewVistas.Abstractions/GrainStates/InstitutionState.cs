// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Institution type — File #4 FACILITY TYPE analog.</summary>
[GenerateSerializer]
public enum InstitutionType
{
    Hospital = 0,
    CriticalAccessHospital = 1,
    Clinic = 2,
    CommunityLivingCenter = 3,
    RehabilitationHospital = 4,
    PsychiatricHospital = 5,
    Other = 99
}

/// <summary>
/// Placement-relevant capability codes — used to filter inter-facility transfer
/// targets ("does the receiving hospital even have an ICU?").
/// </summary>
public static class InstitutionCapabilities
{
    public const string Icu = "ICU";
    public const string Telemetry = "TELEMETRY";
    public const string Pediatrics = "PEDS";
    public const string Nicu = "NICU";
    public const string Obstetrics = "OB";
    public const string BehavioralHealth = "BEHAVIORAL";
    public const string Rehab = "REHAB";
    public const string EmergencyDept = "ED";
}

/// <summary>
/// VistA INSTITUTION file (#4) — one record per facility in the deployment.
/// The FIRST-CLASS home of what was previously loose facility strings
/// ("500", "MAIN", "INST-500"). A health system is a grouping (plain fields),
/// not a grain — promote it if it ever needs behavior. Grain key: "INST:{institutionId}".
/// </summary>
[GenerateSerializer]
public class InstitutionState
{
    /// <summary>Canonical id — the segment used in unit/capacity keys (e.g. "500", "LAHEY-BURLINGTON").</summary>
    [Id(0)] public string InstitutionId { get; set; } = string.Empty;

    /// <summary>NAME (.01).</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    [Id(2)] public InstitutionType Type { get; set; } = InstitutionType.Hospital;

    /// <summary>STATION NUMBER (field 99).</summary>
    [Id(3)] public string? StationNumber { get; set; }

    /// <summary>Health-system grouping (e.g. "BILH") — a plain field, not a grain.</summary>
    [Id(4)] public string? HealthSystemId { get; set; }
    [Id(5)] public string? HealthSystemName { get; set; }

    /// <summary>STREET ADDR. 1 (field 1.01).</summary>
    [Id(6)] public string? StreetAddress { get; set; }
    [Id(7)] public string? City { get; set; }
    [Id(8)] public string? State { get; set; }
    [Id(9)] public string? Zip { get; set; }
    [Id(10)] public string? Phone { get; set; }

    [Id(11)] public bool IsActive { get; set; } = true;

    /// <summary>Transfer-center operational switch — declines are still possible per-request.</summary>
    [Id(12)] public bool AcceptsInboundTransfers { get; set; } = true;

    /// <summary>Placement capabilities — see <see cref="InstitutionCapabilities"/>.</summary>
    [Id(13)] public HashSet<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Legacy facility spellings this institution absorbs (e.g. "MAIN", "INST-500"
    /// for institution "500") so data written before institutions were first-class
    /// still resolves.
    /// </summary>
    [Id(14)] public List<string> LegacyFacilityAliases { get; set; } = new();

    [Id(15)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(16)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Directory entry for the institution index.</summary>
[GenerateSerializer]
public class InstitutionIndexEntry
{
    [Id(0)] public string InstitutionId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public InstitutionType Type { get; set; }
    [Id(3)] public string? HealthSystemId { get; set; }
    [Id(4)] public string? HealthSystemName { get; set; }
    [Id(5)] public string? City { get; set; }
    [Id(6)] public string? State { get; set; }
    [Id(7)] public bool IsActive { get; set; }
    [Id(8)] public bool AcceptsInboundTransfers { get; set; }
    [Id(9)] public HashSet<string> Capabilities { get; set; } = new();
}

/// <summary>
/// Singleton directory of all institutions. Grain key: "INSTITUTION-INDEX".
/// Maintained by InstitutionGrain on register/update (directory-grain pattern).
/// </summary>
[GenerateSerializer]
public class InstitutionIndexState
{
    [Id(0)] public Dictionary<string, InstitutionIndexEntry> Institutions { get; set; } = new();

    /// <summary>Legacy alias → canonical institution id (e.g. "MAIN" → "500").</summary>
    [Id(1)] public Dictionary<string, string> LegacyAliasMap { get; set; } = new();
}
