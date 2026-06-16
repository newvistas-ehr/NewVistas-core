// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single VA Product entry in the National Drug File.
/// Maps to VistA VA PRODUCT file (#50.68) — a specific drug formulation
/// identified by its name, strength, dosage form, and VA generic drug.
///
/// The VA Product is the central reference entity in NDF. Multiple VA Products
/// may share a VA Generic name (e.g., several ATROPINE formulations).
/// VA Products are mapped to NDC/UPN codes in file #50.67.
///
/// Key fields from File #50.68:
///   .01  - NAME (VA Product name, e.g., "ATROPINE SO4 0.4MG TAB")
///   2    - VA GENERIC pointer to file #50.6
///   3    - DOSAGE FORM pointer to file #50.606
///   4    - STRENGTH (numeric)
///   5    - UNITS pointer to file #50.607
///   22   - PRINT NAME
///   25   - PRIMARY VA DRUG CLASS pointer to #50.605
///   31   - NATIONAL FORMULARY INDICATOR (0=no, 1=yes)
///   32   - NATIONAL FORMULARY RESTRICTION
///   33   - CS FEDERAL SCHEDULE
///   45   - MAX SINGLE DOSE
///   46   - MIN SINGLE DOSE
///   47   - MAX DAILY DOSE
///   48   - MIN DAILY DOSE
///   50   - MAX CUMULATIVE DOSE
///   51   - DOSING CHECK EXCLUSION
///   52   - HAZARDOUS WASTE
///   55   - RxNorm code (coding system integration)
///
/// MUMPS entry points: PSNAPIS.m (PROD0, PROD2, DCLASS, VAGN, FORMI, FORMR, DFSU)
/// </summary>
[GenerateSerializer]
public class VaProductState
{
    /// <summary>
    /// VistA IEN in File #50.68. Used as the Orleans grain key.
    /// </summary>
    [Id(0)]
    public string Ien { get; set; } = string.Empty;

    /// <summary>
    /// VA Product name (File #50.68 field .01).
    /// Fully qualified drug name, e.g., "ATROPINE SO4 0.4MG TAB".
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// VA Generic IEN pointer to File #50.6 (field 2).
    /// </summary>
    [Id(2)]
    public string VaGenericIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized VA Generic name for display without grain activation.
    /// e.g., "ATROPINE"
    /// </summary>
    [Id(3)]
    public string VaGenericName { get; set; } = string.Empty;

    /// <summary>
    /// Dosage form IEN pointer to File #50.606 (field 3).
    /// </summary>
    [Id(4)]
    public string DosageFormIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized dosage form name, e.g., "TABLET", "INJECTION", "CAPSULE".
    /// </summary>
    [Id(5)]
    public string DosageFormName { get; set; } = string.Empty;

    /// <summary>
    /// Strength value (File #50.68 field 4), e.g., "0.4", "500", "10".
    /// Stored as string to preserve original VistA formatting.
    /// </summary>
    [Id(6)]
    public string Strength { get; set; } = string.Empty;

    /// <summary>
    /// Strength unit IEN pointer to File #50.607 (field 5).
    /// </summary>
    [Id(7)]
    public string StrengthUnitIen { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized strength unit name, e.g., "MG", "MCG", "MEQ", "ML".
    /// </summary>
    [Id(8)]
    public string StrengthUnitName { get; set; } = string.Empty;

    /// <summary>
    /// Print name for label output (File #50.68 field 22).
    /// Abbreviated display form used on medication labels.
    /// </summary>
    [Id(9)]
    public string PrintName { get; set; } = string.Empty;

    /// <summary>
    /// Primary VA drug class code (pointer to File #50.605, field 25).
    /// e.g., "AM100" = AMINOGLYCOSIDES, "CV000" = CARDIOVASCULAR
    /// </summary>
    [Id(10)]
    public string PrimaryDrugClassCode { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized primary drug class name.
    /// </summary>
    [Id(11)]
    public string PrimaryDrugClassName { get; set; } = string.Empty;

    /// <summary>
    /// Secondary/additional drug class codes for this product.
    /// </summary>
    [Id(12)]
    public List<string> SecondaryDrugClassCodes { get; set; } = new();

    /// <summary>
    /// National Formulary Indicator (File #50.68 field 31).
    /// True = included in the VA National Formulary.
    /// Mirrors $$FORMI^PSNAPIS.
    /// </summary>
    [Id(13)]
    public bool FormularyIndicator { get; set; }

    /// <summary>
    /// National Formulary restrictions text (File #50.68 field 32).
    /// Conditions or limitations on formulary use. May be null if unrestricted.
    /// Mirrors $$FORMR^PSNAPIS.
    /// </summary>
    [Id(14)]
    public string? FormularyRestrictions { get; set; }

    /// <summary>
    /// Controlled substance federal schedule (File #50.68 field 33).
    /// e.g., "2", "3", "4", "5" or null if not scheduled.
    /// </summary>
    [Id(15)]
    public string? ControlledSubstanceSchedule { get; set; }

    /// <summary>
    /// Copay tier designation for VA pharmacy benefit.
    /// e.g., "Tier 1", "Tier 2", "Tier 3".
    /// </summary>
    [Id(16)]
    public string? CopayTier { get; set; }

    /// <summary>
    /// Maximum single dose value (File #50.68, dosing sub-node piece 4).
    /// Null if no limit defined. Unit determined by DoseUnit.
    /// </summary>
    [Id(17)]
    public decimal? MaxSingleDose { get; set; }

    /// <summary>
    /// Minimum single dose value (File #50.68, dosing sub-node piece 5).
    /// </summary>
    [Id(18)]
    public decimal? MinSingleDose { get; set; }

    /// <summary>
    /// Maximum daily dose value (File #50.68, dosing sub-node piece 6).
    /// </summary>
    [Id(19)]
    public decimal? MaxDailyDose { get; set; }

    /// <summary>
    /// Minimum daily dose value (File #50.68, dosing sub-node piece 7).
    /// </summary>
    [Id(20)]
    public decimal? MinDailyDose { get; set; }

    /// <summary>
    /// Maximum cumulative dose (File #50.68, dosing sub-node piece 8).
    /// Used for chemotherapy and other drugs with lifetime limits.
    /// </summary>
    [Id(21)]
    public decimal? MaxCumulativeDose { get; set; }

    /// <summary>
    /// Dose unit for dosing limits (e.g., "MG", "MCG").
    /// </summary>
    [Id(22)]
    public string? DoseUnit { get; set; }

    /// <summary>
    /// Exclusion from dosage order checks (File #50.68 field 51).
    /// True = skip automated dosing validation for this product.
    /// </summary>
    [Id(23)]
    public bool IsDosageCheckExcluded { get; set; }

    /// <summary>
    /// Hazardous waste indicator (File #50.68 field 52).
    /// True = product requires hazardous waste disposal procedures.
    /// </summary>
    [Id(24)]
    public bool IsHazardousWaste { get; set; }

    /// <summary>
    /// RxNorm code from the NLM coding system integration (File #50.68 sub 11).
    /// Enables interoperability with external systems using RxNorm.
    /// </summary>
    [Id(25)]
    public string? RxNormCode { get; set; }

    /// <summary>
    /// VUID — VA Unique Identifier assigned by the VA terminology server.
    /// </summary>
    [Id(26)]
    public string? Vuid { get; set; }

    /// <summary>
    /// Whether this VA Product is currently active in the national catalog.
    /// Inactive products are retained for historical reference.
    /// </summary>
    [Id(27)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Inactivation date if the product has been retired from the formulary.
    /// </summary>
    [Id(28)]
    public DateTime? InactivationDate { get; set; }

    /// <summary>
    /// Active pharmaceutical ingredients for this product.
    /// Maps to the ingredients multiple (sub-node 14P) in File #50.68.
    /// Mirrors $$PSJING^PSNAPIS.
    /// </summary>
    [Id(29)]
    public List<DrugIngredient> Ingredients { get; set; } = new();

    /// <summary>
    /// NDC/UPN codes associated with this VA Product.
    /// Maps to File #50.67 entries pointing back to this product IEN.
    /// </summary>
    [Id(30)]
    public List<NdcEntry> NdcCodes { get; set; } = new();

    [Id(31)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(32)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An active pharmaceutical ingredient within a VA Product.
/// Maps to the INGREDIENTS multiple in VistA File #50.68 sub-node 14P,
/// referencing File #50.416 (DRUG INGREDIENTS).
/// </summary>
[GenerateSerializer]
public class DrugIngredient
{
    /// <summary>
    /// Ingredient IEN from File #50.416.
    /// </summary>
    [Id(0)]
    public string IngredientIen { get; set; } = string.Empty;

    /// <summary>
    /// Ingredient name, e.g., "ATROPINE SULFATE", "CODEINE PHOSPHATE".
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Strength of this ingredient in the product, e.g., "0.4", "500".
    /// </summary>
    [Id(2)]
    public string Strength { get; set; } = string.Empty;

    /// <summary>
    /// Unit for ingredient strength, e.g., "MG", "MCG".
    /// </summary>
    [Id(3)]
    public string StrengthUnit { get; set; } = string.Empty;
}

/// <summary>
/// An NDC (National Drug Code) or UPN (Unit Product Number) associated
/// with a specific commercial package of a VA Product.
/// Maps to VistA NDC/UPN file (#50.67).
/// </summary>
[GenerateSerializer]
public class NdcEntry
{
    /// <summary>
    /// NDC code in standard format (e.g., "0002-7232-50").
    /// The 11-digit NDC identifies labeler, product, and package.
    /// </summary>
    [Id(0)]
    public string NdcCode { get; set; } = string.Empty;

    /// <summary>
    /// UPN (Unit Product Number) — VA-assigned identifier, may be null.
    /// </summary>
    [Id(1)]
    public string? UpnCode { get; set; }

    /// <summary>
    /// Trade/brand name for this specific commercial package.
    /// </summary>
    [Id(2)]
    public string? TradeName { get; set; }

    /// <summary>
    /// Manufacturer/labeler name from File #55.95.
    /// </summary>
    [Id(3)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Package size description, e.g., "100 TABLETS", "10 ML VIAL".
    /// </summary>
    [Id(4)]
    public string? PackageSize { get; set; }

    /// <summary>
    /// Package type, e.g., "BOTTLE", "VIAL", "BLISTER PACK".
    /// </summary>
    [Id(5)]
    public string? PackageType { get; set; }

    /// <summary>
    /// Whether this NDC mapping is currently active.
    /// Inactive NDCs may have been discontinued by the manufacturer.
    /// </summary>
    [Id(6)]
    public bool IsActive { get; set; } = true;
}
