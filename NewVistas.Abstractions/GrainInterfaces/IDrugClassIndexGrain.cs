// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// VA Drug Class Index Grain — singleton holding the in-memory catalog
/// of all VA therapeutic drug classifications from NDF File #50.605.
///
/// Grain Key: "NDF-CLASS-INDEX" (singleton)
///
/// The VA drug classification system uses hierarchical alphanumeric codes.
/// The index supports lookup by code and text search by class name.
///
/// Mirrors VistA storage:
///   ^PS(50.605,ien,0)="code^name"
///   CLASS^PSNAPIS — verify class exists
/// </summary>
public interface IDrugClassIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads drug class entries into the index.
    /// </summary>
    Task LoadClassesAsync(List<DrugClassEntry> entries);

    /// <summary>
    /// Looks up a drug class by its code (e.g., "AM100").
    /// Returns null if not found.
    /// Mirrors CLASS^PSNAPIS.
    /// </summary>
    Task<DrugClassEntry?> GetClassAsync(string code);

    /// <summary>
    /// Returns all drug classes, ordered by code.
    /// </summary>
    Task<List<DrugClassEntry>> GetAllClassesAsync();

    /// <summary>
    /// Searches drug classes by code prefix or name substring.
    /// </summary>
    Task<List<DrugClassEntry>> SearchAsync(string searchText, int maxResults);

    /// <summary>
    /// Returns all child classes of a parent code prefix.
    /// e.g., parent "CV" returns CV000, CV100, CV200, etc.
    /// </summary>
    Task<List<DrugClassEntry>> GetChildClassesAsync(string parentCode);

    /// <summary>
    /// Returns whether the class index has been loaded.
    /// </summary>
    Task<bool> IsLoadedAsync();

    /// <summary>
    /// Returns index load status.
    /// </summary>
    Task<NdfClassIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the index.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the drug class index.
/// </summary>
[GenerateSerializer]
public class NdfClassIndexStatus
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    [Id(2)]
    public int TotalClasses { get; set; }
}
