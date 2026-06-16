// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the VA Generic Index grain — holds the in-memory searchable
/// catalog of all VA Generic drug names from NDF File #50.6.
///
/// There are ~5,417 VA Generic entries (5,377 active). This list maps
/// generic pharmaceutical names to their VA Product formulations.
///
/// Cross-references from VistA:
///   ^PSNDF(50.6,"B",name,ien) — lookup by generic name
///   PSNAPIS VAP entry point   — all products for a generic
/// </summary>
[GenerateSerializer]
public class VaGenericIndexState
{
    /// <summary>
    /// All loaded VA Generic entries, keyed by IEN.
    /// </summary>
    [Id(0)]
    public Dictionary<string, VaGenericEntry> GenericsByIen { get; set; } = new();

    /// <summary>
    /// Whether the index has been loaded.
    /// </summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// When the index was last loaded.
    /// </summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>
    /// Total number of VA Generic entries.
    /// </summary>
    [Id(3)]
    public int TotalGenerics { get; set; }

    /// <summary>
    /// Number of currently active VA Generic entries.
    /// </summary>
    [Id(4)]
    public int ActiveGenerics { get; set; }
}

/// <summary>
/// A VA Generic drug entry from NDF File #50.6.
/// Represents the generic pharmaceutical name at the top of the
/// NDF hierarchy: Generic → Products → NDC codes.
/// </summary>
[GenerateSerializer]
public class VaGenericEntry
{
    /// <summary>
    /// VistA IEN in File #50.6.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// VA Generic drug name (File #50.6 field .01).
    /// e.g., "ATROPINE", "AMOXICILLIN", "METOPROLOL".
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// VA Unique Identifier from the VA terminology server.
    /// </summary>
    [Id(2)]
    public string? Vuid { get; set; }

    /// <summary>
    /// Whether this generic is currently active in the NDF.
    /// </summary>
    [Id(3)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// IENs of VA Products (File #50.68) associated with this generic.
    /// Populated during index load from the product index.
    /// </summary>
    [Id(4)]
    public List<string> ProductIens { get; set; } = new();
}
