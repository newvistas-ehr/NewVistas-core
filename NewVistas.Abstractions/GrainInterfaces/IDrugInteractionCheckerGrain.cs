// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Drug Interaction Checker Grain — StatelessWorker that detects all drug-drug
/// interactions in a set of active pharmaceutical ingredients.
///
/// Grain Key: "CHECKER" (key is ignored by StatelessWorker routing; use any constant)
///
/// This grain carries no persistent state. It reads interaction pairs from
/// IDrugInteractionCacheService (silo-level singleton) for lock-free, O(n²)
/// pairwise checking. Orleans may maintain multiple local activations per silo
/// under load, all sharing the same singleton cache instance.
///
/// Call pattern (mirrors DRGINT^PSO interaction check in VistA CPRS):
///   1. Collect all DrugIngredient objects from the patient's active prescriptions.
///   2. Call CheckInteractionsAsync with the complete ingredient list.
///   3. Surface returned DrugInteractionResult objects as clinical alerts.
///
/// The checker does NOT modify any state. It is safe to call concurrently.
/// </summary>
public interface IDrugInteractionCheckerGrain : IGrainWithStringKey
{
    /// <summary>
    /// Checks all pairwise combinations of the provided drug ingredients
    /// against the silo-level interaction cache.
    ///
    /// Returns one DrugInteractionResult per interacting pair found.
    /// Pairs with no known interaction are omitted. An empty list means
    /// no interactions were detected.
    ///
    /// Self-interaction (same IngredientIen appearing more than once,
    /// e.g., two products containing the same active ingredient) is skipped.
    ///
    /// Time complexity: O(n²) on the number of distinct ingredient IENs.
    /// Typical medication lists (5–15 drugs) make this negligible.
    /// </summary>
    Task<List<DrugInteractionResult>> CheckInteractionsAsync(List<DrugIngredient> ingredients);

    /// <summary>
    /// Returns true when the silo-level cache contains at least one interaction pair.
    /// Use to detect whether IDrugInteractionDatasetGrain has been loaded yet.
    /// </summary>
    Task<bool> IsCacheReadyAsync();
}
