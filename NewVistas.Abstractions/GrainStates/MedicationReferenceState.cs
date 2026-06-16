// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A standard medication administration route from VistA File #51.23
/// (STANDARD MEDICATION ROUTES). These 55 canonical routes are used
/// throughout pharmacy and CPRS for order entry and MAR documentation.
///
/// Key fields from File #51.23:
///   .01  - NAME (e.g., "ORAL", "INTRAVENOUS", "SUBCUTANEOUS")
///   1    - ABBREVIATION (e.g., "PO", "IV", "SC")
///   99.99 - VUID (VA Unique Identifier from the terminology server)
/// </summary>
[GenerateSerializer]
public class MedicationRoute
{
    /// <summary>
    /// VistA IEN in File #51.23.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// Standard route name (File #51.23, field .01).
    /// e.g., "ORAL", "INTRAVENOUS", "INTRAMUSCULAR", "SUBCUTANEOUS"
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Route abbreviation used on MARs and labels (File #51.23, field 1).
    /// e.g., "PO", "IV", "IM", "SC", "TOP", "INH".
    /// Null for routes without a standard abbreviation.
    /// </summary>
    [Id(2)]
    public string? Abbreviation { get; set; }

    /// <summary>
    /// VA Unique Identifier from the VA terminology server (VUID field).
    /// Enables cross-system terminology mapping.
    /// </summary>
    [Id(3)]
    public string Vuid { get; set; } = string.Empty;

    /// <summary>
    /// Whether this route is currently active for use in order entry.
    /// All 55 seeded routes from VistA are active.
    /// </summary>
    [Id(4)]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Persistent state for the MedicationRouteIndexGrain singleton (key: "MED-ROUTE-INDEX").
/// Self-seeded from VistA ZWR data on first activation — no admin load required.
/// </summary>
[GenerateSerializer]
public class MedicationRouteIndexState
{
    /// <summary>All medication routes keyed by VistA IEN.</summary>
    [Id(0)]
    public Dictionary<string, MedicationRoute> ByIen { get; set; } = new();

    /// <summary>
    /// Secondary index: route name (uppercase) → IEN for fast name lookup.
    /// e.g., "ORAL" → "22"
    /// </summary>
    [Id(1)]
    public Dictionary<string, string> IenByName { get; set; } = new();

    /// <summary>
    /// True once the grain has been seeded from VistA static data.
    /// Prevents re-seeding on subsequent activations.
    /// </summary>
    [Id(2)]
    public bool IsLoaded { get; set; }
}

/// <summary>
/// A dose unit from VistA File #51.24 (DOSE UNITS).
/// These 54 canonical units quantify medication doses in order entry,
/// labels, and dosing calculation (e.g., "MILLIGRAM(S)", "TABLET(S)").
///
/// Key fields from File #51.24:
///   .01  - NAME (e.g., "MILLIGRAM(S)", "TABLET(S)", "MILLILITER(S)")
///   1    - ABBREVIATION (e.g., "MG", "TAB", "ML")
///   2    - PLURAL (e.g., "MILLIGRAMS", "TABLETS")
///   3    - ABBREVIATION (alternate short form)
/// </summary>
[GenerateSerializer]
public class DoseUnit
{
    /// <summary>
    /// VistA IEN in File #51.24.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// Full dose unit name as used in VistA (File #51.24, field .01).
    /// e.g., "MILLIGRAM(S)", "TABLET(S)", "MILLILITER(S)", "INHALATION(S)"
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Standard abbreviation for this unit (File #51.24, field 1).
    /// e.g., "MG", "TAB", "ML", "INH", "MCG", "MEQ"
    /// </summary>
    [Id(2)]
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a discrete counting unit (true) vs. mass/volume unit (false).
    /// True for: TABLET(S), CAPSULE(S), PATCH(ES), SUPPOSITORY(IES), etc.
    /// False for: MILLIGRAM(S), MILLILITER(S), MICROGRAM(S), GRAM(S), etc.
    /// Sourced from VistA ZWR piece 3 where "1" = counting unit.
    /// </summary>
    [Id(3)]
    public bool IsCounting { get; set; }
}

/// <summary>
/// Persistent state for the DoseUnitIndexGrain singleton (key: "DOSE-UNIT-INDEX").
/// Self-seeded from VistA ZWR data on first activation — no admin load required.
/// </summary>
[GenerateSerializer]
public class DoseUnitIndexState
{
    /// <summary>All dose units keyed by VistA IEN.</summary>
    [Id(0)]
    public Dictionary<string, DoseUnit> ByIen { get; set; } = new();

    /// <summary>
    /// Secondary index: unit name (uppercase) → IEN for fast name lookup.
    /// e.g., "MILLIGRAM(S)" → "19"
    /// </summary>
    [Id(1)]
    public Dictionary<string, string> IenByName { get; set; } = new();

    /// <summary>
    /// True once the grain has been seeded from VistA static data.
    /// Prevents re-seeding on subsequent activations.
    /// </summary>
    [Id(2)]
    public bool IsLoaded { get; set; }
}
