// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the VA Product Index grain — holds the in-memory searchable
/// catalog of all VA Products from NDF File #50.68.
///
/// Follows the same pattern as Icd10IndexState: lightweight entries for
/// fast search without activating individual VaProductGrains.
///
/// Cross-references mirrored from VistA:
///   ^PSNDF(50.68,"B",name,ien)  — by product name
///   ^PSNDF(50.67,"NDC",ndc,ien) — NDC to product mapping
///   PSNAPIS VAP entry point     — all products for a generic
/// </summary>
[GenerateSerializer]
public class VaProductIndexState
{
    /// <summary>
    /// All loaded VA Product entries, keyed by product IEN.
    /// </summary>
    [Id(0)]
    public Dictionary<string, VaProductIndexEntry> ProductsByIen { get; set; } = new();

    /// <summary>
    /// Reverse mapping from NDC code to product IEN for fast NDC lookup.
    /// Mirrors ^PSNDF(50.67,"NDC") cross-reference.
    /// </summary>
    [Id(1)]
    public Dictionary<string, string> IenByNdc { get; set; } = new();

    /// <summary>
    /// Mapping from VA Generic IEN to list of product IENs.
    /// Mirrors the VAP entry point in PSNAPIS.m.
    /// </summary>
    [Id(2)]
    public Dictionary<string, List<string>> IensByGenericIen { get; set; } = new();

    /// <summary>
    /// Whether the index has been loaded.
    /// </summary>
    [Id(3)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// When the index was last loaded.
    /// </summary>
    [Id(4)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>
    /// Total number of VA Products in the index.
    /// </summary>
    [Id(5)]
    public int TotalProducts { get; set; }

    /// <summary>
    /// Number of products currently in the VA National Formulary.
    /// </summary>
    [Id(6)]
    public int FormularyProducts { get; set; }
}

/// <summary>
/// Lightweight VA Product entry for index search — no grain activation needed.
/// Contains the fields most commonly needed for search results and selection.
/// </summary>
[GenerateSerializer]
public class VaProductIndexEntry
{
    /// <summary>
    /// VistA IEN in File #50.68. Used to activate the full VaProductGrain.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// Full VA Product name, e.g., "ATROPINE SO4 0.4MG TAB".
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// VA Generic IEN pointer to File #50.6.
    /// </summary>
    [Id(2)]
    public string VaGenericIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized VA Generic name for display, e.g., "ATROPINE".
    /// </summary>
    [Id(3)]
    public string VaGenericName { get; set; } = string.Empty;

    /// <summary>
    /// Dosage form name, e.g., "TABLET", "INJECTION".
    /// </summary>
    [Id(4)]
    public string DosageFormName { get; set; } = string.Empty;

    /// <summary>
    /// Strength value, e.g., "0.4", "500".
    /// </summary>
    [Id(5)]
    public string Strength { get; set; } = string.Empty;

    /// <summary>
    /// Strength unit name, e.g., "MG", "MCG".
    /// </summary>
    [Id(6)]
    public string StrengthUnitName { get; set; } = string.Empty;

    /// <summary>
    /// Primary drug class code, e.g., "AM100".
    /// </summary>
    [Id(7)]
    public string PrimaryDrugClassCode { get; set; } = string.Empty;

    /// <summary>
    /// Primary drug class name.
    /// </summary>
    [Id(8)]
    public string PrimaryDrugClassName { get; set; } = string.Empty;

    /// <summary>
    /// True if this product is in the VA National Formulary.
    /// </summary>
    [Id(9)]
    public bool FormularyIndicator { get; set; }

    /// <summary>
    /// True if this product is currently active in the NDF catalog.
    /// </summary>
    [Id(10)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// RxNorm code for external system interoperability.
    /// </summary>
    [Id(11)]
    public string? RxNormCode { get; set; }

    /// <summary>
    /// NDC codes associated with this product (denormalized for display).
    /// </summary>
    [Id(12)]
    public List<string> NdcCodes { get; set; } = new();

    /// <summary>
    /// Controlled substance federal schedule, null if not scheduled.
    /// </summary>
    [Id(13)]
    public string? ControlledSubstanceSchedule { get; set; }
}
