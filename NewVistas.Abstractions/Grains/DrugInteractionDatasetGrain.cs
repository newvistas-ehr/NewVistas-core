// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Drug Interaction Dataset Grain — persistent singleton store for all known
/// drug-drug interaction pairs. Keyed "DI-DATASET".
///
/// Responsibilities:
///   - Persist the interaction pair dictionary to durable storage.
///   - Maintain the silo-level IDrugInteractionCacheService in sync.
///   - Re-populate the cache on activation so StatelessWorker checkers can
///     operate immediately after a silo restart without waiting for a reload.
/// </summary>
public class DrugInteractionDatasetGrain : Grain, IDrugInteractionDatasetGrain
{
    private readonly IPersistentState<DrugInteractionDatasetState> _state;
    private readonly IDrugInteractionCacheService _cacheService;

    public DrugInteractionDatasetGrain(
        [PersistentState("drugInteractionDataset", "drugInteractionStore")]
        IPersistentState<DrugInteractionDatasetState> state,
        IDrugInteractionCacheService cacheService)
    {
        _state = state;
        _cacheService = cacheService;
    }

    /// <summary>
    /// On activation: if the dataset was previously loaded, push the persisted
    /// pairs into the cache so checkers don't start with an empty cache.
    /// </summary>
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.IsLoaded && _state.State.PairsByKey.Count > 0)
            _cacheService.Swap(new Dictionary<string, DrugInteractionPair>(_state.State.PairsByKey));

        return base.OnActivateAsync(cancellationToken);
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

        await _state.WriteStateAsync();

        // Swap the cache atomically so StatelessWorker checkers see new data.
        _cacheService.Swap(new Dictionary<string, DrugInteractionPair>(_state.State.PairsByKey));
    }

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
            IsCachePopulated = _cacheService.IsPopulated
        });

    public async Task ClearAsync()
    {
        _state.State.PairsByKey.Clear();
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        _state.State.TotalPairs = 0;

        await _state.WriteStateAsync();

        // Reset the cache to empty so checkers stop firing alerts.
        _cacheService.Swap(new Dictionary<string, DrugInteractionPair>());
    }
}
