// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the CPT Code Index grain.
/// Holds the searchable catalog of all CPT codes in memory.
/// </summary>
[GenerateSerializer]
public class CptCodeIndexState
{
    /// <summary>
    /// All loaded CPT code entries, keyed by code.
    /// </summary>
    [Id(0)]
    public Dictionary<string, CptCodeIndexEntry> Codes { get; set; } = new();

    /// <summary>
    /// Whether the index has been loaded.
    /// </summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// When the index was last loaded/refreshed.
    /// </summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>
    /// Total number of codes in the index.
    /// </summary>
    [Id(3)]
    public int TotalCodes { get; set; }

    /// <summary>
    /// Number of active codes in the index.
    /// </summary>
    [Id(4)]
    public int ActiveCodes { get; set; }
}

/// <summary>
/// Lightweight CPT code index entry for search — no grain activation needed.
/// </summary>
[GenerateSerializer]
public class CptCodeIndexEntry
{
    /// <summary>
    /// CPT code value, e.g., "99213".
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Short name / descriptor.
    /// </summary>
    [Id(1)]
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Full description of the procedure.
    /// </summary>
    [Id(2)]
    public string LongDescription { get; set; } = string.Empty;

    /// <summary>
    /// CPT category, e.g., "E&M", "Surgery".
    /// </summary>
    [Id(3)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Work RVU value.
    /// </summary>
    [Id(4)]
    public decimal WorkRvu { get; set; }

    /// <summary>
    /// Total RVU value.
    /// </summary>
    [Id(5)]
    public decimal TotalRvu { get; set; }

    /// <summary>
    /// Code status (ACTIVE or INACTIVE).
    /// </summary>
    [Id(6)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Effective date for this code version.
    /// </summary>
    [Id(7)]
    public DateTime? EffectiveDate { get; set; }
}
