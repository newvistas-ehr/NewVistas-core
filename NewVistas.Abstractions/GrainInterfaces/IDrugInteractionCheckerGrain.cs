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
/// This grain carries no persistent state. It validates the silo-local
/// IDrugInteractionCacheService against the DI-DATASET grain's version on
/// every check (one tiny call) and pull-through-populates the cache when it
/// is missing or stale — so checkers on EVERY silo see current data.
///
/// FAIL-CLOSED CONTRACT: when the dataset is not loaded, the response status
/// is DataUnavailable. Callers must block the fill/order — never interpret
/// an unavailable check as "no interactions found".
///
/// Call pattern (mirrors DRGINT^PSO interaction check in VistA CPRS):
///   1. Collect all DrugIngredient objects from the patient's active prescriptions.
///   2. Call CheckInteractionsAsync with the complete ingredient list.
///   3. If Status is DataUnavailable, block. Otherwise surface Results as alerts.
///
/// The checker does NOT modify any state. It is safe to call concurrently.
/// </summary>
public interface IDrugInteractionCheckerGrain : IGrainWithStringKey
{
    /// <summary>
    /// Checks all pairwise combinations of the provided drug ingredients
    /// against the (version-validated) interaction dataset.
    ///
    /// Returns Ok with one DrugInteractionResult per interacting pair found
    /// (empty Results = no interactions detected), or DataUnavailable when
    /// the dataset has not been loaded — in which case the caller must
    /// fail closed.
    ///
    /// Self-interaction (same IngredientIen appearing more than once,
    /// e.g., two products containing the same active ingredient) is skipped.
    ///
    /// Time complexity: O(n²) on the number of distinct ingredient IENs.
    /// Typical medication lists (5–15 drugs) make this negligible.
    /// </summary>
    Task<DrugInteractionCheckResponse> CheckInteractionsAsync(List<DrugIngredient> ingredients);

    /// <summary>
    /// Returns true when the interaction dataset has been loaded. Cluster-correct:
    /// queries the DI-DATASET grain rather than any silo-local cache.
    /// </summary>
    Task<bool> IsCacheReadyAsync();
}
