// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single dosage option available for ordering via a Pharmacy Orderable Item.
/// Corresponds to the POSSIBLE DOSAGES multiple in File #50.7 sub-node 3.
/// These are the dosage choices presented to providers in CPRS during order entry.
/// </summary>
[GenerateSerializer]
public class OrderableDosage
{
    /// <summary>
    /// Dosage amount string displayed to the provider, e.g., "325MG", "500MG/5ML".
    /// </summary>
    [Id(0)]
    public string Dose { get; set; } = string.Empty;

    /// <summary>
    /// Dispense unit for this dosage option, e.g., "TABLET", "ML".
    /// </summary>
    [Id(1)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Medication route for this dosage option (e.g., "ORAL", "INTRAVENOUS").
    /// Null if not constrained to a specific route.
    /// </summary>
    [Id(2)]
    public string? Route { get; set; }

    /// <summary>
    /// Whether this is the default dosage option presented first in CPRS.
    /// </summary>
    [Id(3)]
    public bool IsDefault { get; set; }
}

/// <summary>
/// A Pharmacy Orderable Item — the clinical ordering entity that CPRS presents
/// to providers during medication order entry. Maps to VistA File #50.7.
///
/// In VistA, providers order against Orderable Items, not individual drug entries.
/// The pharmacy then maps each Orderable Item to one or more specific Drug entries
/// (File #50) for dispensing. This separation allows clinical ordering to be
/// independent of the facility's local drug packaging choices.
///
/// Key fields from File #50.7:
///   .01  - NAME (orderable item name, e.g., "ASPIRIN TAB")
///   2    - PHARMACY TYPE
///   3    - VA GENERIC pointer to #50.6
///   4    - DOSAGE FORM NAME (e.g., "TABLET")
///   5    - PRIMARY VA DRUG CLASS pointer to #50.605
///   100  - INACTIVE DATE
///   101  - NATIONAL FORMULARY INDICATOR
///   3    - POSSIBLE DOSAGES multiple (sub-node)
///   104  - OUTPATIENT EXPANSION sub-node (DAYS SUPPLY, QUANTITY)
///   25   - DRUG multiple (links to File #50 entries)
/// </summary>
[GenerateSerializer]
public class OrderableItemState
{
    /// <summary>
    /// VistA IEN in File #50.7. Used as the Orleans grain key.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// Orderable item name as displayed in CPRS (File #50.7, field .01).
    /// e.g., "ASPIRIN TAB", "METFORMIN HCL TAB", "LISINOPRIL TAB"
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pharmacy type for this orderable item (File #50.7, field 2).
    /// Determines which pharmacy service handles orders for this item.
    /// </summary>
    [Id(2)]
    public PharmacyType PharmacyType { get; set; } = PharmacyType.Outpatient;

    /// <summary>
    /// VA Generic IEN pointer to File #50.6 (field 3).
    /// Links the orderable item to its generic drug class in NDF.
    /// </summary>
    [Id(3)]
    public string VaGenericIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized VA Generic name (e.g., "ASPIRIN", "METFORMIN").
    /// Stored to avoid NDF cross-grain lookups during CPRS order display.
    /// </summary>
    [Id(4)]
    public string VaGenericName { get; set; } = string.Empty;

    /// <summary>
    /// Dosage form name for this item (File #50.7, field 4).
    /// e.g., "TABLET", "CAPSULE", "INJECTION", "SOLUTION"
    /// </summary>
    [Id(5)]
    public string DosageFormName { get; set; } = string.Empty;

    /// <summary>
    /// Primary VA drug class code (File #50.7, field 5).
    /// </summary>
    [Id(6)]
    public string PrimaryDrugClassCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether this orderable item is currently active for ordering in CPRS.
    /// </summary>
    [Id(7)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date this orderable item was inactivated (File #50.7, field 100).
    /// Null when the item is active.
    /// </summary>
    [Id(8)]
    public DateTime? InactivationDate { get; set; }

    /// <summary>
    /// Whether this orderable item is on the National Formulary (field 101).
    /// Sourced from the linked NDF VA Generic formulary status.
    /// </summary>
    [Id(9)]
    public bool IsNationalFormulary { get; set; }

    /// <summary>
    /// Available dosage options presented to providers in CPRS (File #50.7, sub 3).
    /// Configured by pharmacy during formulary setup.
    /// </summary>
    [Id(10)]
    public List<OrderableDosage> AvailableDosages { get; set; } = new();

    /// <summary>
    /// IENs of File #50 Drug entries linked to this orderable item (sub 25).
    /// A single orderable item may map to multiple drug packaging variants.
    /// e.g., ASPIRIN 325MG TAB → aspirin 325mg regular tab AND aspirin 325mg EC tab
    /// </summary>
    [Id(11)]
    public List<string> LinkedDrugIens { get; set; } = new();

    /// <summary>
    /// Default days supply for outpatient prescriptions (File #50.7, sub 104).
    /// Null means the global formulary default applies.
    /// </summary>
    [Id(12)]
    public int? OutpatientDefaultDaysSupply { get; set; }

    /// <summary>
    /// Default quantity for outpatient prescriptions (File #50.7, sub 104).
    /// Null means no local default; provider enters quantity manually.
    /// </summary>
    [Id(13)]
    public decimal? OutpatientDefaultQuantity { get; set; }

    /// <summary>
    /// Synonym names for alternate name lookups and order search.
    /// </summary>
    [Id(14)]
    public List<string> Synonyms { get; set; } = new();

    /// <summary>Date this orderable item entry was created.</summary>
    [Id(15)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this orderable item entry was last modified.</summary>
    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight projection of an orderable item for index grain search results.
/// </summary>
[GenerateSerializer]
public class OrderableItemIndexEntry
{
    /// <summary>VistA IEN in File #50.7.</summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>Orderable item name (e.g., "ASPIRIN TAB").</summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>VA Generic IEN pointer to File #50.6.</summary>
    [Id(2)]
    public string VaGenericIen { get; set; } = string.Empty;

    /// <summary>Denormalized VA Generic name.</summary>
    [Id(3)]
    public string VaGenericName { get; set; } = string.Empty;

    /// <summary>Primary drug class code.</summary>
    [Id(4)]
    public string PrimaryDrugClassCode { get; set; } = string.Empty;

    /// <summary>Pharmacy type (outpatient/unit dose/both/IV/all).</summary>
    [Id(5)]
    public PharmacyType PharmacyType { get; set; } = PharmacyType.Outpatient;

    /// <summary>Whether the orderable item is currently active.</summary>
    [Id(6)]
    public bool IsActive { get; set; } = true;

    /// <summary>Whether the item is on the national formulary.</summary>
    [Id(7)]
    public bool IsNationalFormulary { get; set; }
}

/// <summary>
/// Persistent state for the OrderableItemIndexGrain singleton (key: "OI-INDEX").
/// Holds all orderable items as a Dictionary keyed by IEN.
/// </summary>
[GenerateSerializer]
public class OrderableItemIndexState
{
    /// <summary>All orderable item index entries keyed by VistA IEN.</summary>
    [Id(0)]
    public Dictionary<string, OrderableItemIndexEntry> ByIen { get; set; } = new();

    /// <summary>Whether the index has been populated with at least one load.</summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>UTC timestamp of the most recent bulk load.</summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>Total number of orderable items in the index.</summary>
    [Id(3)]
    public int TotalItems { get; set; }
}

/// <summary>
/// Status summary for the orderable item index grain.
/// </summary>
[GenerateSerializer]
public class OrderableItemIndexStatus
{
    /// <summary>Whether the index has been loaded.</summary>
    [Id(0)]
    public bool IsLoaded { get; set; }

    /// <summary>UTC timestamp of the last bulk load.</summary>
    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>Total orderable items in the index.</summary>
    [Id(2)]
    public int TotalItems { get; set; }
}
