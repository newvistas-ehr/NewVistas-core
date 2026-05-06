// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Drug Interaction Dataset Grain — persistent singleton store for all known
/// drug-drug interaction pairs.
///
/// Grain Key: "DI-DATASET" (singleton)
///
/// Maps to VistA Drug Interaction files #56-1 through #56-6 (~401 MB).
/// Interactions are defined at the ingredient level (File #50.416) so that
/// all products containing a flagged ingredient are automatically covered.
///
/// On activation, the grain repopulates the silo-level IDrugInteractionCacheService
/// so that IDrugInteractionCheckerGrain StatelessWorker instances can check
/// orders from the in-memory cache without touching persistent storage on every call.
///
/// MUMPS heritage: DRGINT.m, PSO*5.0*212 interaction check routines.
/// </summary>
public interface IDrugInteractionDatasetGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads interaction pairs into the dataset, replacing all prior data.
    /// Immediately swaps the silo-level cache so checkers see the new data.
    /// </summary>
    Task LoadInteractionsAsync(List<DrugInteractionPair> pairs);

    /// <summary>
    /// Returns the interaction record for two ingredients, or null if no
    /// interaction is known. Lookup is order-independent.
    /// </summary>
    Task<DrugInteractionPair?> GetInteractionAsync(string ingredientIen1, string ingredientIen2);

    /// <summary>
    /// Returns all known interaction pairs in the dataset.
    /// </summary>
    Task<List<DrugInteractionPair>> GetAllInteractionsAsync();

    /// <summary>
    /// Returns dataset load status and statistics.
    /// </summary>
    Task<DrugInteractionStatus> GetStatusAsync();

    /// <summary>
    /// Clears all interaction data and resets the silo-level cache to empty.
    /// </summary>
    Task ClearAsync();
}
