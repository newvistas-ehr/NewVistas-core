// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for a single Pharmacy Orderable Item (VistA File #50.7).
/// Keyed by the orderable item's VistA IEN (e.g., "521").
///
/// Orderable Items are the clinical ordering entities that CPRS presents
/// to providers during medication order entry. Each grain manages the full
/// state of one orderable item, including its linked drug entries,
/// available dosage options, and outpatient ordering defaults.
///
/// Corresponds to VistA PSS package routines:
///   PSSOI.m   — Orderable item utilities
///   PSSOEDIT.m — Orderable item edit entry points
/// </summary>
public interface IPharmacyOrderableItemGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns the full orderable item state.
    /// </summary>
    Task<OrderableItemState> GetItemAsync();

    /// <summary>
    /// Persists a complete orderable item state. Sets LastModifiedDate to UTC now.
    /// </summary>
    Task SaveItemAsync(OrderableItemState item);

    /// <summary>
    /// Links a facility drug entry (File #50 IEN) to this orderable item.
    /// Deduplicates — if the drug IEN is already linked, the call is a no-op.
    /// </summary>
    Task AddLinkedDrugAsync(string drugIen);

    /// <summary>
    /// Removes a linked drug IEN. No-op if the IEN was not linked.
    /// </summary>
    Task RemoveLinkedDrugAsync(string drugIen);

    /// <summary>
    /// Appends a dosage option to the available dosages list.
    /// Deduplicates by Dose+Unit+Route combination.
    /// </summary>
    Task AddDosageAsync(OrderableDosage dosage);

    /// <summary>
    /// Marks the orderable item as inactive with the given date (field 100).
    /// Inactive items are hidden from CPRS order entry.
    /// </summary>
    Task DeactivateAsync(DateTime inactivationDate);
}
