// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the VA Drug Class Index grain — holds the in-memory catalog
/// of all VA drug therapeutic classifications from NDF File #50.605.
///
/// Drug classes use a hierarchical coding scheme:
///   e.g., "AM100" = AMINOGLYCOSIDES (subclass of AM = ANTI-INFECTIVE AGENTS)
///
/// VistA stores these in ^PS(50.605,ien,0)="code^name".
/// </summary>
[GenerateSerializer]
public class DrugClassIndexState
{
    /// <summary>
    /// All loaded drug class entries, keyed by class code (e.g., "AM100").
    /// </summary>
    [Id(0)]
    public Dictionary<string, DrugClassEntry> ClassesByCode { get; set; } = new();

    /// <summary>
    /// Whether the class index has been loaded.
    /// </summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// When the class index was last loaded.
    /// </summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>
    /// Total number of drug classes.
    /// </summary>
    [Id(3)]
    public int TotalClasses { get; set; }
}

/// <summary>
/// A VA therapeutic drug classification entry from NDF File #50.605.
/// The VA drug classification system is a proprietary hierarchy used
/// to group drugs by pharmacological action and therapeutic use.
///
/// Examples:
///   "AM100" - AMINOGLYCOSIDES
///   "CV000" - CARDIOVASCULAR AGENTS
///   "CV100" - ANTIARRHYTHMIC AGENTS
///   "CV200" - ANTIHYPERTENSIVE AGENTS
///   "HS000" - HORMONES/SYNTHETICS/MODIFIERS
/// </summary>
[GenerateSerializer]
public class DrugClassEntry
{
    /// <summary>
    /// VA drug class code (File #50.605 field .01).
    /// Alphanumeric code, e.g., "AM100", "CV200".
    /// Used as the dictionary key and grain lookup value.
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Drug class name (File #50.605 field 1).
    /// e.g., "AMINOGLYCOSIDES", "ANTIHYPERTENSIVE AGENTS".
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parent class code derived from the code prefix.
    /// e.g., "AM100" parent is "AM" (ANTI-INFECTIVE AGENTS).
    /// Null for top-level classes.
    /// </summary>
    [Id(2)]
    public string? ParentCode { get; set; }

    /// <summary>
    /// Whether this drug class is currently active.
    /// </summary>
    [Id(3)]
    public bool IsActive { get; set; } = true;
}
