// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the ICD-10 Index grain.
/// Holds the searchable catalog of all ICD-10-CM codes in memory.
/// This mirrors how VistA's ^ICD9 global has B and BA cross-references
/// for fast lookup by code and text.
/// </summary>
[GenerateSerializer]
public class Icd10IndexState
{
    /// <summary>
    /// All loaded ICD-10 code entries, keyed by code.
    /// </summary>
    [Id(0)]
    public Dictionary<string, Icd10IndexEntry> Codes { get; set; } = new();

    /// <summary>
    /// Whether the index has been loaded from the CMS order file.
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
    /// Number of billable codes in the index.
    /// </summary>
    [Id(4)]
    public int BillableCodes { get; set; }
}

/// <summary>
/// Lightweight index entry for search — no grain activation needed.
/// </summary>
[GenerateSerializer]
public class Icd10IndexEntry
{
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    [Id(1)]
    public string ShortDescription { get; set; } = string.Empty;

    [Id(2)]
    public string LongDescription { get; set; } = string.Empty;

    [Id(3)]
    public bool IsBillable { get; set; }

    [Id(4)]
    public int OrderNumber { get; set; }

    [Id(5)]
    public string? Chapter { get; set; }

    [Id(6)]
    public bool IsActive { get; set; } = true;
}
