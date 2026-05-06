// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Drug Interaction Checker Grain — StatelessWorker that checks a list of drug
/// ingredients against the silo-level interaction cache.
///
/// [StatelessWorker] allows Orleans to maintain multiple local activations per
/// silo under load. All activations share the same IDrugInteractionCacheService
/// singleton, so they all read from the same interaction snapshot.
///
/// This grain has no persistent state and performs no writes. It is purely
/// a computation kernel that wraps the cache lookup in Orleans grain semantics.
/// </summary>
[StatelessWorker]
public class DrugInteractionCheckerGrain : Grain, IDrugInteractionCheckerGrain
{
    private readonly IDrugInteractionCacheService _cacheService;

    public DrugInteractionCheckerGrain(IDrugInteractionCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public Task<List<DrugInteractionResult>> CheckInteractionsAsync(List<DrugIngredient> ingredients)
    {
        // Need at least two distinct ingredients to form a pair.
        if (ingredients.Count < 2)
            return Task.FromResult(new List<DrugInteractionResult>());

        IReadOnlyDictionary<string, DrugInteractionPair> snapshot = _cacheService.GetSnapshot();

        if (snapshot.Count == 0)
            return Task.FromResult(new List<DrugInteractionResult>());

        List<DrugInteractionResult> results = new();

        // Evaluate every unique pair (n*(n-1)/2 combinations).
        for (int i = 0; i < ingredients.Count - 1; i++)
        {
            for (int j = i + 1; j < ingredients.Count; j++)
            {
                DrugIngredient a = ingredients[i];
                DrugIngredient b = ingredients[j];

                // Skip entries without a usable IEN.
                if (string.IsNullOrEmpty(a.IngredientIen) || string.IsNullOrEmpty(b.IngredientIen))
                    continue;

                // Skip self-interaction — same ingredient appearing in multiple products.
                if (a.IngredientIen == b.IngredientIen)
                    continue;

                string key = DrugInteractionKeyHelper.MakePairKey(a.IngredientIen, b.IngredientIen);

                if (snapshot.TryGetValue(key, out DrugInteractionPair? pair))
                {
                    results.Add(new DrugInteractionResult
                    {
                        Drug1 = a,
                        Drug2 = b,
                        Interaction = pair
                    });
                }
            }
        }

        return Task.FromResult(results);
    }

    public Task<bool> IsCacheReadyAsync() =>
        Task.FromResult(_cacheService.IsPopulated);
}
