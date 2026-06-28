// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A pharmacy in the facility's pharmacy directory — the dispensing destination for a
/// prescription. Outpatient (retail/mail/specialty) pharmacies are what a patient chooses
/// between; the inpatient (hospital) pharmacy is the only option for inpatient orders.
///
/// A pharmacy is, for e-prescribing purposes, just an address you route a NewRx to: the
/// US standard is NCPDP SCRIPT over the Surescripts network, and the pharmacy is identified
/// by its <see cref="NcpdpId"/> (7-digit NCPDP Provider ID) and/or <see cref="Npi"/>. The
/// protocol is the same regardless of chain (CVS / Walgreens / independent / mail-order).
/// </summary>
[GenerateSerializer]
public record PharmacyDirectoryEntry
{
    /// <summary>Internal directory id / grain reference (e.g. "PHARM-CVS-4501").</summary>
    [Id(0)]
    public string PharmacyId { get; init; } = string.Empty;

    /// <summary>Display name, e.g. "CVS PHARMACY #4501".</summary>
    [Id(1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Kind of pharmacy: RETAIL, MAIL, INPATIENT, or SPECIALTY. Inpatient pharmacies are the
    /// hospital's own and are excluded from the outpatient (patient-choice) list.
    /// </summary>
    [Id(2)]
    public string Kind { get; init; } = PharmacyKinds.Retail;

    /// <summary>7-digit NCPDP Provider ID — the e-prescribing routing address. Null for the
    /// internal hospital pharmacy (dispensed in-house, not via NCPDP SCRIPT).</summary>
    [Id(3)]
    public string? NcpdpId { get; init; }

    /// <summary>National Provider Identifier (10-digit), if known.</summary>
    [Id(4)]
    public string? Npi { get; init; }

    [Id(5)] public string? Address { get; init; }
    [Id(6)] public string? City { get; init; }
    [Id(7)] public string? State { get; init; }
    [Id(8)] public string? Zip { get; init; }
    [Id(9)] public string? Phone { get; init; }
    [Id(10)] public string? Fax { get; init; }

    /// <summary>True if the pharmacy can receive electronic prescriptions (NCPDP SCRIPT).</summary>
    [Id(11)]
    public bool AcceptsErx { get; init; } = true;

    /// <summary>False for closed/inactive pharmacies — excluded from search.</summary>
    [Id(12)]
    public bool IsActive { get; init; } = true;
}

/// <summary>Well-known <see cref="PharmacyDirectoryEntry.Kind"/> values.</summary>
public static class PharmacyKinds
{
    public const string Retail = "RETAIL";
    public const string Mail = "MAIL";
    public const string Inpatient = "INPATIENT";
    public const string Specialty = "SPECIALTY";
}

/// <summary>
/// Singleton state for the <c>PharmacyDirectoryGrain</c> (key "PHARMACY-DIRECTORY") — a
/// facility-wide directory of pharmacies keyed by <see cref="PharmacyDirectoryEntry.PharmacyId"/>.
/// Mirrors the ProviderDirectory / ClinicIndex singleton pattern.
/// </summary>
[GenerateSerializer]
public class PharmacyDirectoryState
{
    /// <summary>All pharmacies keyed by PharmacyId.</summary>
    [Id(0)]
    public Dictionary<string, PharmacyDirectoryEntry> Pharmacies { get; set; } = new();
}
