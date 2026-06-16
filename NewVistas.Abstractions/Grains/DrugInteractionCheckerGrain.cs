// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Drug Interaction Checker Grain — StatelessWorker that checks a list of drug
/// ingredients against the version-validated interaction dataset.
///
/// [StatelessWorker] allows Orleans to maintain multiple local activations per
/// silo under load. All activations share the same IDrugInteractionCacheService
/// singleton, which this grain pull-through-populates: on each check it
/// compares the cached snapshot version against DI-DATASET (one tiny call) and
/// pulls a fresh snapshot only when the cache is missing or stale. Because the
/// pull happens on the silo receiving the call, every silo self-populates.
///
/// FAIL-CLOSED: when the dataset is not loaded, returns DataUnavailable —
/// never an empty "no interactions" result. An empty silo cache previously
/// passed every check silently (fail-open), which on a multi-silo cluster
/// meant unchecked medication orders.
/// </summary>
[StatelessWorker]
public class DrugInteractionCheckerGrain : Grain, IDrugInteractionCheckerGrain
{
    private readonly IDrugInteractionCacheService _cacheService;

    public DrugInteractionCheckerGrain(IDrugInteractionCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    private IDrugInteractionDatasetGrain Dataset()
        => GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");

    public async Task<DrugInteractionCheckResponse> CheckInteractionsAsync(List<DrugIngredient> ingredients)
    {
        // Need at least two distinct ingredients to form a pair — legitimately
        // nothing to check, not a data-availability problem.
        if (ingredients.Count < 2)
            return new DrugInteractionCheckResponse { Status = DrugInteractionCheckStatus.Ok };

        DrugInteractionDatasetVersion current = await Dataset().GetVersionAsync();

        if (!current.IsLoaded)
            return new DrugInteractionCheckResponse { Status = DrugInteractionCheckStatus.DataUnavailable };

        CachedInteractionSnapshot? cached = _cacheService.GetSnapshot();
        if (cached is null || cached.Version != current.Version)
        {
            DrugInteractionSnapshot fresh = await Dataset().GetSnapshotAsync();

            // Re-check: a ClearAsync may have raced between the version probe
            // and the snapshot pull.
            if (!fresh.IsLoaded)
                return new DrugInteractionCheckResponse { Status = DrugInteractionCheckStatus.DataUnavailable };

            cached = new CachedInteractionSnapshot(fresh.Version, fresh.PairsByKey);
            _cacheService.Swap(cached);
        }

        IReadOnlyDictionary<string, DrugInteractionPair> snapshot = cached.Pairs;
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

        return new DrugInteractionCheckResponse
        {
            Status = DrugInteractionCheckStatus.Ok,
            Results = results,
            DatasetVersion = cached.Version
        };
    }

    public async Task<bool> IsCacheReadyAsync()
        => (await Dataset().GetVersionAsync()).IsLoaded;
}
