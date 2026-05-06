// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Ward Stock Item Grain — manages one drug's inventory at one ward location.
/// Supports auto-replenishment: when quantity drops to or below the reorder point,
/// a replenishment request is automatically generated.
/// </summary>
public class WardStockItemGrain : Grain, IWardStockItemGrain
{
    private readonly IPersistentState<WardStockItemState> _state;

    public WardStockItemGrain(
        [PersistentState("wardStockItem", "wardStockItemStore")] IPersistentState<WardStockItemState> state)
    {
        _state = state;
    }

    public Task<WardStockItemState> GetItemAsync() => Task.FromResult(_state.State);

    public async Task SetupItemAsync(string drugId, string drugName, string wardId, string wardName,
        int parLevel, int reorderPoint, int reorderQuantity, string unitOfMeasure,
        bool isControlledSubstance)
    {
        _state.State.DrugId = drugId;
        _state.State.DrugName = drugName;
        _state.State.WardId = wardId;
        _state.State.WardName = wardName;
        _state.State.ParLevel = parLevel;
        _state.State.ReorderPoint = reorderPoint;
        _state.State.ReorderQuantity = reorderQuantity;
        _state.State.UnitOfMeasure = unitOfMeasure;
        _state.State.IsControlledSubstance = isControlledSubstance;
        _state.State.QuantityOnHand = parLevel; // Start at par
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<bool> DispenseAsync(int quantity)
    {
        if (_state.State.QuantityOnHand < quantity)
            return false;

        _state.State.QuantityOnHand -= quantity;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        // Check auto-replenishment trigger
        if (_state.State.AutoReplenishEnabled &&
            !_state.State.ReplenishmentPending &&
            _state.State.QuantityOnHand <= _state.State.ReorderPoint)
        {
            await TriggerAutoReplenishmentAsync();
        }

        return true;
    }

    public async Task ReceiveAsync(int quantity)
    {
        _state.State.QuantityOnHand += quantity;
        _state.State.LastReplenishmentDate = DateTime.UtcNow;
        _state.State.ReplenishmentPending = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetParLevelAsync(int parLevel, int reorderPoint, int reorderQuantity)
    {
        _state.State.ParLevel = parLevel;
        _state.State.ReorderPoint = reorderPoint;
        _state.State.ReorderQuantity = reorderQuantity;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetAutoReplenishAsync(bool enabled)
    {
        _state.State.AutoReplenishEnabled = enabled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordInventoryCountAsync(int actualCount)
    {
        _state.State.QuantityOnHand = actualCount;
        _state.State.LastInventoryDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkReplenishmentPendingAsync()
    {
        _state.State.ReplenishmentPending = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ClearReplenishmentPendingAsync()
    {
        _state.State.ReplenishmentPending = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private async Task TriggerAutoReplenishmentAsync()
    {
        _state.State.ReplenishmentPending = true;
        await _state.WriteStateAsync();

        // Log the replenishment request
        var log = GrainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{_state.State.WardId}");
        await log.AddRequestAsync(new ReplenishmentRequest
        {
            RequestId = $"REPL-{Guid.NewGuid():N}",
            WardId = _state.State.WardId,
            DrugId = _state.State.DrugId,
            DrugName = _state.State.DrugName,
            QuantityRequested = _state.State.ReorderQuantity,
            Status = "PENDING",
            RequestDate = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Ward Stock Index Grain — lightweight index of all drugs at a ward.
/// </summary>
public class WardStockIndexGrain : Grain, IWardStockIndexGrain
{
    private readonly IPersistentState<WardStockIndexState> _state;

    public WardStockIndexGrain(
        [PersistentState("wardStockIndex", "wardStockIndexStore")] IPersistentState<WardStockIndexState> state)
    {
        _state = state;
    }

    public Task<List<WardStockSummaryEntry>> GetAllItemsAsync()
        => Task.FromResult(_state.State.Items);

    public Task<List<WardStockSummaryEntry>> GetLowStockItemsAsync()
        => Task.FromResult(_state.State.Items.Where(i => i.NeedsReplenishment).ToList());

    public async Task AddOrUpdateAsync(WardStockSummaryEntry entry)
    {
        int idx = _state.State.Items.FindIndex(i => i.DrugId == entry.DrugId);
        if (idx >= 0) _state.State.Items[idx] = entry;
        else _state.State.Items.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string drugId)
    {
        _state.State.Items.RemoveAll(i => i.DrugId == drugId);
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Ward Replenishment Log Grain — request log per ward.
/// </summary>
public class WardReplenishmentLogGrain : Grain, IWardReplenishmentLogGrain
{
    private readonly IPersistentState<WardReplenishmentLogState> _state;

    public WardReplenishmentLogGrain(
        [PersistentState("wardReplenishLog", "wardReplenishLogStore")] IPersistentState<WardReplenishmentLogState> state)
    {
        _state = state;
    }

    public Task<List<ReplenishmentRequest>> GetRequestsAsync()
        => Task.FromResult(_state.State.Requests.OrderByDescending(r => r.RequestDate).ToList());

    public Task<List<ReplenishmentRequest>> GetPendingRequestsAsync()
        => Task.FromResult(_state.State.Requests.Where(r => r.Status == "PENDING").ToList());

    public async Task AddRequestAsync(ReplenishmentRequest request)
    {
        _state.State.Requests.Add(request);
        await _state.WriteStateAsync();
    }

    public async Task FillRequestAsync(string requestId)
    {
        var req = _state.State.Requests.Find(r => r.RequestId == requestId);
        if (req != null)
        {
            req.Status = "FILLED";
            req.FilledDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task CancelRequestAsync(string requestId)
    {
        var req = _state.State.Requests.Find(r => r.RequestId == requestId);
        if (req != null)
        {
            req.Status = "CANCELLED";
            await _state.WriteStateAsync();
        }
    }
}
