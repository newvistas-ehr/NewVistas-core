// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for a single facility-level drug entry (VistA File #50).
/// Keyed by the drug's VistA IEN (e.g., "1324").
///
/// Each grain instance represents one row in the local drug catalog.
/// The drug grain manages the full drug state including possible dosages,
/// synonyms, and NDF linkage data.
///
/// Corresponds to VistA PSS (Pharmacy Data Management) package routines:
///   PSSDRG.m — Drug file utilities
///   PSSDRGEE.m — Drug file edit entry points
/// </summary>
public interface IDrugGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns the full drug state for this entry.
    /// </summary>
    Task<DrugState> GetDrugAsync();

    /// <summary>
    /// Persists a complete drug state. Sets LastModifiedDate to UTC now.
    /// Used for both initial creation and full updates.
    /// </summary>
    Task SaveDrugAsync(DrugState drug);

    /// <summary>
    /// Marks the drug as inactive with the given date (File #50, field 200).
    /// Inactive drugs remain in the catalog for historical reference but
    /// cannot be dispensed or ordered.
    /// </summary>
    Task DeactivateAsync(DateTime inactivationDate);

    /// <summary>
    /// Reactivates a previously inactivated drug, clearing the inactivation date.
    /// </summary>
    Task ReactivateAsync();

    /// <summary>
    /// Appends a possible dosage option to this drug entry (File #50, sub 903).
    /// Avoids duplicates — if a dosage with the same Dose+Unit already exists,
    /// the call is a no-op.
    /// </summary>
    Task AddPossibleDosageAsync(PossibleDosage dosage);

    /// <summary>
    /// Returns the active pharmaceutical ingredients for this drug.
    /// Used by the drug interaction checker to identify interaction-relevant
    /// ingredients without activating the full NDF VA Product grain.
    /// </summary>
    Task<List<DrugIngredient>> GetIngredientsAsync();
}
