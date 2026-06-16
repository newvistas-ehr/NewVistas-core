// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
/// Distribution model (pull-through): this grain is the versioned source of
/// truth. IDrugInteractionCheckerGrain StatelessWorker activations validate
/// their silo-local cache against GetVersionAsync on every check and pull
/// GetSnapshotAsync only when missing or stale — so every silo in the
/// cluster self-populates, and freshness is version-exact (never time-based).
///
/// MUMPS heritage: DRGINT.m, PSO*5.0*212 interaction check routines.
/// </summary>
public interface IDrugInteractionDatasetGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads interaction pairs into the dataset, replacing all prior data
    /// and bumping the dataset version so checkers on every silo re-pull.
    /// </summary>
    Task LoadInteractionsAsync(List<DrugInteractionPair> pairs);

    /// <summary>
    /// Merges interaction pairs into the dataset without removing existing
    /// pairs (incremental update), marking the dataset loaded and bumping the
    /// version. Existing entries with the same canonical pair key are replaced.
    /// </summary>
    Task AddInteractionsAsync(List<DrugInteractionPair> pairs);

    /// <summary>
    /// Returns the current dataset version and load flag. Tiny payload —
    /// called once per interaction check to validate silo-local caches.
    /// </summary>
    Task<DrugInteractionDatasetVersion> GetVersionAsync();

    /// <summary>
    /// Returns a versioned copy of the full dataset for silo-local caching.
    /// </summary>
    Task<DrugInteractionSnapshot> GetSnapshotAsync();

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
    /// Clears all interaction data and bumps the version. Checkers detect the
    /// cleared dataset on their next version check and fail closed.
    /// </summary>
    Task ClearAsync();
}
