// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// VA Product Index Grain — singleton holding the searchable in-memory catalog
/// of all VA Products from NDF File #50.68.
///
/// Grain Key: "NDF-PRODUCT-INDEX" (singleton)
///
/// This mirrors VistA cross-references on ^PSNDF(50.68):
///   "B" xref (by product name) — for name-based search
///   "NDC" xref on ^PSNDF(50.67) — for NDC lookup
///   VAP^PSNAPIS — all products for a generic drug
///
/// The index is populated via the bulk load endpoint. Once loaded,
/// all searches are purely in-memory O(n) scans against the dictionary.
/// For 29,584 products this is fast enough; a production system could
/// add inverted indexes if needed.
/// </summary>
public interface IVaProductIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads VA Product entries into the index.
    /// Called by the controller after parsing NDF data.
    /// Replaces any previously loaded entries.
    /// </summary>
    Task LoadProductsAsync(List<VaProductIndexEntry> entries);

    /// <summary>
    /// Looks up a single VA Product by its IEN. Returns null if not found.
    /// </summary>
    Task<VaProductIndexEntry?> GetProductAsync(string ien);

    /// <summary>
    /// Looks up a VA Product by NDC code. Returns null if not found.
    /// Mirrors ^PSNDF(50.67,"NDC") cross-reference in VistA.
    /// </summary>
    Task<VaProductIndexEntry?> GetByNdcAsync(string ndcCode);

    /// <summary>
    /// Returns all VA Products associated with a given VA Generic IEN.
    /// Mirrors the VAP^PSNAPIS entry point which returns all products
    /// for a generic, including name, dosage form, class, and tier.
    /// </summary>
    Task<List<VaProductIndexEntry>> GetByGenericAsync(string vaGenericIen, int maxResults = 100);

    /// <summary>
    /// Returns all VA Products in a given drug class.
    /// Mirrors DCLASS^PSNAPIS lookup.
    /// </summary>
    Task<List<VaProductIndexEntry>> GetByDrugClassAsync(string drugClassCode, bool activeOnly = true, int maxResults = 200);

    /// <summary>
    /// Searches VA Products by name, generic name, or NDC code.
    ///
    /// If the search text matches an NDC pattern (digits and hyphens),
    /// performs an NDC lookup. Otherwise searches product name and
    /// generic name case-insensitively.
    ///
    /// Mirrors PSNLOOK.m drug selection and PSNCOMP.m matching strategies.
    /// </summary>
    Task<List<VaProductIndexEntry>> SearchAsync(
        string searchText,
        bool formularyOnly,
        string? drugClassCode,
        bool activeOnly,
        int maxResults);

    /// <summary>
    /// Returns index load status and statistics.
    /// </summary>
    Task<NdfProductIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the index. Used before a reload.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the VA Product index.
/// </summary>
[GenerateSerializer]
public class NdfProductIndexStatus
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    [Id(2)]
    public int TotalProducts { get; set; }

    [Id(3)]
    public int FormularyProducts { get; set; }

    [Id(4)]
    public int ActiveProducts { get; set; }
}
