// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// VA Product Grain — represents a single entry in VistA NDF
/// VA PRODUCT file (#50.68), the primary drug formulation record.
///
/// Grain Key: VistA IEN as string (e.g., "1234").
///
/// This grain stores the full reference data for one VA Product formulation.
/// It is loaded from NDF data and read by clinical grains (Pharmacy, BCMA,
/// Orders) that need drug details, formulary status, and dosing limits.
///
/// Related VistA APIs:
///   PROD0^PSNAPIS  — retrieve product 0-node data
///   PROD2^PSNAPIS  — retrieve product 1-node data
///   DCLASS^PSNAPIS — get drug class for product
///   VAGN^PSNAPIS   — get VA generic name
///   FORMI^PSNAPIS  — get formulary indicator
///   FORMR^PSNAPIS  — get formulary restrictions
///   DFSU^PSNAPIS   — get dose form, strength, units
///   PSJING^PSNAPIS — get ingredients
///   CPRS^PSNAPIS   — CPRS format (dosform^class^strength^units)
/// </summary>
public interface IVaProductGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns the full VA Product state.
    /// </summary>
    Task<VaProductState> GetProductAsync();

    /// <summary>
    /// Loads or updates all fields for this VA Product.
    /// Called during NDF data import. Mirrors VistA NDF update routines.
    /// </summary>
    Task LoadProductAsync(
        string name,
        string vaGenericIen,
        string vaGenericName,
        string dosageFormIen,
        string dosageFormName,
        string strength,
        string strengthUnitIen,
        string strengthUnitName,
        string printName,
        string primaryDrugClassCode,
        string primaryDrugClassName,
        bool formularyIndicator,
        string? formularyRestrictions,
        string? controlledSubstanceSchedule,
        string? copayTier,
        decimal? maxSingleDose,
        decimal? minSingleDose,
        decimal? maxDailyDose,
        decimal? minDailyDose,
        decimal? maxCumulativeDose,
        string? doseUnit,
        bool isDosageCheckExcluded,
        bool isHazardousWaste,
        string? rxNormCode,
        string? vuid,
        bool isActive,
        DateTime? inactivationDate,
        List<DrugIngredient> ingredients,
        List<NdcEntry> ndcCodes,
        List<string> secondaryDrugClassCodes);

    /// <summary>
    /// Returns the formulary indicator.
    /// True if this product is in the VA National Formulary.
    /// Mirrors $$FORMI^PSNAPIS.
    /// </summary>
    Task<bool> IsFormularyAsync();

    /// <summary>
    /// Returns the formulary restriction text, or null if unrestricted.
    /// Mirrors $$FORMR^PSNAPIS.
    /// </summary>
    Task<string?> GetFormularyRestrictionsAsync();

    /// <summary>
    /// Returns dosage form, strength, and unit as a display string.
    /// e.g., "TABLET 0.4 MG".
    /// Mirrors $$DFSU^PSNAPIS.
    /// </summary>
    Task<string> GetDosageStrengthSummaryAsync();

    /// <summary>
    /// Returns CPRS-format drug data: "dosform^class^strength^units".
    /// Mirrors $$CPRS^PSNAPIS for order entry integration.
    /// </summary>
    Task<string> GetCprsFormatAsync();

    /// <summary>
    /// Returns whether this product is currently active.
    /// </summary>
    Task<bool> IsActiveAsync();

    /// <summary>
    /// Marks this product as active in the NDF catalog.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Marks this product as inactive (e.g., discontinued formulation).
    /// </summary>
    Task DeactivateAsync(DateTime? inactivationDate);

    /// <summary>
    /// Adds an NDC/UPN entry to this product's package list.
    /// Called when new commercial packages are mapped to this VA Product.
    /// </summary>
    Task AddNdcEntryAsync(NdcEntry ndcEntry);

    /// <summary>
    /// Updates the formulary status of this product.
    /// </summary>
    Task UpdateFormularyStatusAsync(bool inFormulary, string? restrictions, string? copayTier);
}
