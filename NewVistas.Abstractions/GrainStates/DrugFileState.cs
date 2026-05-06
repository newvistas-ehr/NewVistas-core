// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Pharmacy type classification for a Drug entry (File #50, field .07).
/// Determines which pharmacy service manages this drug.
/// </summary>
[GenerateSerializer]
public enum PharmacyType
{
    /// <summary>Outpatient pharmacy only.</summary>
    Outpatient = 1,

    /// <summary>Unit dose (inpatient) pharmacy only.</summary>
    UnitDose = 2,

    /// <summary>Both outpatient and unit dose.</summary>
    Both = 3,

    /// <summary>IV room / admixtures.</summary>
    IV = 4,

    /// <summary>All pharmacy services.</summary>
    All = 5
}

/// <summary>
/// A possible dosage option configured for a facility drug entry.
/// Corresponds to the POSSIBLE DOSAGES multiple in File #50 sub-node 903.
/// Pharmacists configure these; they appear in the orderable item dosage picker.
/// </summary>
[GenerateSerializer]
public class PossibleDosage
{
    /// <summary>
    /// Dosage amount string, e.g., "325MG", "500MG", "10MG/5ML".
    /// </summary>
    [Id(0)]
    public string Dose { get; set; } = string.Empty;

    /// <summary>
    /// Dispense unit label, e.g., "TABLET", "CAPSULE", "ML".
    /// </summary>
    [Id(1)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the default dosage selection for this drug.
    /// </summary>
    [Id(2)]
    public bool IsDefault { get; set; }
}

/// <summary>
/// Facility-level drug entry — the local drug catalog for a VA medical center.
/// Maps to VistA DRUG file (#50). Each entry represents a specific drug
/// formulation stocked and dispensed at this facility, linked to its
/// national NDF counterpart in File #50.68.
///
/// Key fields from File #50:
///   .01  - GENERIC NAME (local drug name, e.g., "ASPIRIN TAB,EC 325MG")
///   2    - VA GENERIC NAME pointer to #50.6
///   20   - NATIONAL DRUG FILE ENTRY pointer to #50.68 (VA Product)
///   22   - VA PRODUCT NAME (denormalized)
///   25   - PRIMARY VA DRUG CLASS pointer to #50.605
///   100  - PHARMACY TYPE (outpatient/unit-dose/both/IV/all)
///   200  - INACTIVE DATE
///   203  - MESSAGE
///   204  - LOCAL MESSAGE
///   503  - CMOP DISPENSE (0=local, 1=CMOP, 2=both)
///   510  - DEA SPECIAL HANDLING
///   511  - CS FEDERAL SCHEDULE
///   906  - NDC (local)
///   901  - DISPENSE UNIT
///   903  - POSSIBLE DOSAGES (multiple)
/// </summary>
[GenerateSerializer]
public class DrugState
{
    /// <summary>
    /// VistA IEN in File #50. Used as the Orleans grain key.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// Local generic name of the drug (File #50, field .01).
    /// e.g., "ASPIRIN TAB,EC 325MG", "METFORMIN HCL TAB 500MG"
    /// </summary>
    [Id(1)]
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// Pointer to VA Product in NDF File #50.68 (field 20).
    /// Links this local drug entry to its national formulary counterpart.
    /// </summary>
    [Id(2)]
    public string VaProductIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized VA Product name from File #50.68 (field 22).
    /// Stored to avoid cross-grain lookups for display purposes.
    /// </summary>
    [Id(3)]
    public string VaProductName { get; set; } = string.Empty;

    /// <summary>
    /// Pointer to VA Generic in File #50.6 (field 2).
    /// </summary>
    [Id(4)]
    public string VaGenericIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized VA Generic name (e.g., "ASPIRIN", "METFORMIN").
    /// </summary>
    [Id(5)]
    public string VaGenericName { get; set; } = string.Empty;

    /// <summary>
    /// Primary VA drug class code from File #50.605 (field 25).
    /// e.g., "HS000" = HORMONES/SYNTHETICS/MODIFIERS, "CV000" = CARDIOVASCULAR
    /// </summary>
    [Id(6)]
    public string PrimaryDrugClassCode { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized primary drug class name.
    /// </summary>
    [Id(7)]
    public string PrimaryDrugClassName { get; set; } = string.Empty;

    /// <summary>
    /// Pharmacy service that manages this drug (File #50, field .07 / 100).
    /// </summary>
    [Id(8)]
    public PharmacyType PharmacyType { get; set; } = PharmacyType.Outpatient;

    /// <summary>
    /// Whether the drug is currently active and available for dispensing.
    /// </summary>
    [Id(9)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date the drug was inactivated (File #50, field 200).
    /// Null when the drug is currently active.
    /// </summary>
    [Id(10)]
    public DateTime? InactivationDate { get; set; }

    /// <summary>
    /// Maximum days supply allowed per prescription (field from outpatient expansion).
    /// Null means no local limit defined (formulary rules apply).
    /// </summary>
    [Id(11)]
    public int? MaxDaysSupply { get; set; }

    /// <summary>
    /// Drug message shown at dispensing time (File #50, field 203).
    /// Pharmacist-facing alert or note.
    /// </summary>
    [Id(12)]
    public string? Message { get; set; }

    /// <summary>
    /// Local message for this drug (File #50, field 204).
    /// Facility-specific note, may differ from national message.
    /// </summary>
    [Id(13)]
    public string? LocalMessage { get; set; }

    /// <summary>
    /// CMOP dispense flag (File #50, field 503).
    /// 0=dispense locally, 1=CMOP only, 2=both local and CMOP.
    /// </summary>
    [Id(14)]
    public int CmopDispense { get; set; }

    /// <summary>
    /// DEA special handling code (File #50, field 510).
    /// e.g., "S" = Schedule drug, "N" = Narcotic.
    /// </summary>
    [Id(15)]
    public string? DeaSpecialHandling { get; set; }

    /// <summary>
    /// Controlled substance federal schedule (File #50, field 511).
    /// e.g., "2", "3", "4", "5" — null if not scheduled.
    /// </summary>
    [Id(16)]
    public string? ControlledSubstanceSchedule { get; set; }

    /// <summary>
    /// Local NDC code for this drug (File #50, field 906).
    /// May differ from NDF NDC if local repackaging occurred.
    /// </summary>
    [Id(17)]
    public string? NdcCode { get; set; }

    /// <summary>
    /// Dispense unit for this drug (File #50, field 901).
    /// e.g., "TABLET", "ML", "CAPSULE".
    /// </summary>
    [Id(18)]
    public string? DispenseUnit { get; set; }

    /// <summary>
    /// Configured possible dosages for order entry (File #50, sub 903).
    /// Populated by pharmacy during formulary configuration.
    /// </summary>
    [Id(19)]
    public List<PossibleDosage> PossibleDosages { get; set; } = new();

    /// <summary>
    /// Local synonym names for this drug (File #50, sub 1 "Synonyms").
    /// Used to support alternate name lookups.
    /// </summary>
    [Id(20)]
    public List<string> Synonyms { get; set; } = new();

    /// <summary>
    /// Active pharmaceutical ingredients for drug-interaction checking.
    /// Sourced from the linked NDF VA Product (File #50.68 sub 14P).
    /// Stored here to avoid NDF cross-grain calls during interaction checks.
    /// </summary>
    [Id(21)]
    public List<DrugIngredient> Ingredients { get; set; } = new();

    /// <summary>
    /// True if the linked VA Product is on the National Formulary (from NDF field 31).
    /// </summary>
    [Id(22)]
    public bool IsNationalFormulary { get; set; }

    /// <summary>
    /// True if this drug has been added to the local facility formulary
    /// as a non-formulary exception (facility-level flag).
    /// </summary>
    [Id(23)]
    public bool IsLocalNonFormulary { get; set; }

    /// <summary>Date this drug entry was created in the local drug file.</summary>
    [Id(24)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this drug entry was last modified.</summary>
    [Id(25)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight projection of a drug entry used in the index grain.
/// Contains only the fields needed for search and list display.
/// </summary>
[GenerateSerializer]
public class DrugIndexEntry
{
    /// <summary>VistA IEN in File #50.</summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>Local generic name (File #50, field .01).</summary>
    [Id(1)]
    public string LocalName { get; set; } = string.Empty;

    /// <summary>Pointer to VA Product in File #50.68.</summary>
    [Id(2)]
    public string VaProductIen { get; set; } = string.Empty;

    /// <summary>Pointer to VA Generic in File #50.6.</summary>
    [Id(3)]
    public string VaGenericIen { get; set; } = string.Empty;

    /// <summary>Denormalized VA Generic name.</summary>
    [Id(4)]
    public string VaGenericName { get; set; } = string.Empty;

    /// <summary>Primary drug class code.</summary>
    [Id(5)]
    public string PrimaryDrugClassCode { get; set; } = string.Empty;

    /// <summary>Pharmacy type (outpatient/unit dose/both/IV/all).</summary>
    [Id(6)]
    public PharmacyType PharmacyType { get; set; } = PharmacyType.Outpatient;

    /// <summary>Whether the drug is currently active.</summary>
    [Id(7)]
    public bool IsActive { get; set; } = true;

    /// <summary>Whether the drug is on the national formulary.</summary>
    [Id(8)]
    public bool IsNationalFormulary { get; set; }

    /// <summary>Local synonym names for alternate-name search.</summary>
    [Id(9)]
    public List<string> Synonyms { get; set; } = new();
}

/// <summary>
/// Persistent state for the DrugIndexGrain singleton (key: "DRUG-INDEX").
/// Holds the entire facility drug catalog as a Dictionary keyed by IEN
/// for O(1) retrieval and fast linear search.
/// </summary>
[GenerateSerializer]
public class DrugIndexState
{
    /// <summary>All drug index entries keyed by VistA IEN.</summary>
    [Id(0)]
    public Dictionary<string, DrugIndexEntry> ByIen { get; set; } = new();

    /// <summary>Whether the index has been populated with at least one load.</summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>UTC timestamp of the most recent bulk load.</summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>Total number of drugs in the index.</summary>
    [Id(3)]
    public int TotalDrugs { get; set; }
}

/// <summary>
/// Status summary for the drug index grain, returned by GetStatusAsync.
/// </summary>
[GenerateSerializer]
public class DrugIndexStatus
{
    /// <summary>Whether the index has been loaded.</summary>
    [Id(0)]
    public bool IsLoaded { get; set; }

    /// <summary>UTC timestamp of the last bulk load.</summary>
    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>Total drugs in the index.</summary>
    [Id(2)]
    public int TotalDrugs { get; set; }
}
