// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Drug Interaction Dataset Grain — persistent singleton store for all known
/// drug-drug interaction pairs. Keyed "DI-DATASET".
///
/// Responsibilities:
///   - Persist the interaction pair dictionary to durable storage.
///   - Serve versioned snapshots so StatelessWorker checkers on EVERY silo
///     can pull-through-populate their silo-local caches. (The previous
///     push-based design only populated the cache on the silo hosting this
///     grain — checkers on other silos silently saw an empty cache.)
///   - Bump Version on every load/clear so checker caches are version-exact.
/// </summary>
public class DrugInteractionDatasetGrain : Grain, IDrugInteractionDatasetGrain
{
    private readonly IPersistentState<DrugInteractionDatasetState> _state;

    public DrugInteractionDatasetGrain(
        [PersistentState("drugInteractionDataset", "drugInteractionStore")]
        IPersistentState<DrugInteractionDatasetState> state)
    {
        _state = state;
    }

    public async Task LoadInteractionsAsync(List<DrugInteractionPair> pairs)
    {
        _state.State.PairsByKey.Clear();

        foreach (DrugInteractionPair pair in pairs)
        {
            // Normalize to canonical order before storing so reads by either
            // ordering always land on the same key.
            string key = DrugInteractionKeyHelper.MakePairKey(pair.IngredientIen1, pair.IngredientIen2);
            _state.State.PairsByKey[key] = pair;
        }

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalPairs = _state.State.PairsByKey.Count;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddInteractionsAsync(List<DrugInteractionPair> pairs)
    {
        foreach (DrugInteractionPair pair in pairs)
        {
            string key = DrugInteractionKeyHelper.MakePairKey(pair.IngredientIen1, pair.IngredientIen2);
            _state.State.PairsByKey[key] = pair;
        }

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalPairs = _state.State.PairsByKey.Count;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<DrugInteractionDatasetVersion> GetVersionAsync() =>
        Task.FromResult(new DrugInteractionDatasetVersion
        {
            Version = _state.State.Version,
            IsLoaded = _state.State.IsLoaded
        });

    public Task<DrugInteractionSnapshot> GetSnapshotAsync() =>
        Task.FromResult(new DrugInteractionSnapshot
        {
            Version = _state.State.Version,
            IsLoaded = _state.State.IsLoaded,
            // Copy: the snapshot crosses silo boundaries and is cached by readers.
            PairsByKey = new Dictionary<string, DrugInteractionPair>(_state.State.PairsByKey)
        });

    public Task<DrugInteractionPair?> GetInteractionAsync(string ingredientIen1, string ingredientIen2)
    {
        string key = DrugInteractionKeyHelper.MakePairKey(ingredientIen1, ingredientIen2);
        _state.State.PairsByKey.TryGetValue(key, out DrugInteractionPair? pair);
        return Task.FromResult(pair);
    }

    public Task<List<DrugInteractionPair>> GetAllInteractionsAsync() =>
        Task.FromResult(_state.State.PairsByKey.Values.ToList());

    public Task<DrugInteractionStatus> GetStatusAsync() =>
        Task.FromResult(new DrugInteractionStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalPairs = _state.State.TotalPairs,
            // Pull-through caching: data availability mirrors IsLoaded on every silo.
            IsCachePopulated = _state.State.IsLoaded,
            Version = _state.State.Version
        });

    public async Task ClearAsync()
    {
        _state.State.PairsByKey.Clear();
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        _state.State.TotalPairs = 0;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }
}
