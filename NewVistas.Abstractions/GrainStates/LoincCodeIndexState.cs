// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the LOINC Code Index grain.
/// Holds the searchable catalog of all LOINC codes in memory.
/// </summary>
[GenerateSerializer]
public class LoincCodeIndexState
{
    /// <summary>
    /// All loaded LOINC code entries, keyed by LOINC code number.
    /// </summary>
    [Id(0)]
    public Dictionary<string, LoincCodeIndexEntry> Codes { get; set; } = new();

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
/// Lightweight LOINC code index entry for search — no grain activation needed.
/// </summary>
[GenerateSerializer]
public class LoincCodeIndexEntry
{
    /// <summary>
    /// LOINC code number, e.g., "2345-7".
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Component (analyte), e.g., "Glucose".
    /// </summary>
    [Id(1)]
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Property, e.g., "MCnc".
    /// </summary>
    [Id(2)]
    public string Property { get; set; } = string.Empty;

    /// <summary>
    /// Time aspect, e.g., "Pt".
    /// </summary>
    [Id(3)]
    public string TimeAspect { get; set; } = string.Empty;

    /// <summary>
    /// System (specimen), e.g., "Ser/Plas".
    /// </summary>
    [Id(4)]
    public string System { get; set; } = string.Empty;

    /// <summary>
    /// Scale, e.g., "Qn".
    /// </summary>
    [Id(5)]
    public string Scale { get; set; } = string.Empty;

    /// <summary>
    /// Method, may be empty.
    /// </summary>
    [Id(6)]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Short name for display.
    /// </summary>
    [Id(7)]
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Long common name.
    /// </summary>
    [Id(8)]
    public string LongCommonName { get; set; } = string.Empty;

    /// <summary>
    /// Code status: ACTIVE, TRIAL, DISCOURAGED, DEPRECATED.
    /// </summary>
    [Id(9)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Class type categorization.
    /// </summary>
    [Id(10)]
    public string ClassType { get; set; } = string.Empty;
}
