// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Ward Stock Item Grain — manages one drug's inventory at one ward.
/// Key: "WARD-STOCK:{wardId}:{drugId}"
/// Maps to VistA PSGW (Ward Stock) package.
/// </summary>
public interface IWardStockItemGrain : IGrainWithStringKey
{
    Task<WardStockItemState> GetItemAsync();

    Task SetupItemAsync(string drugId, string drugName, string wardId, string wardName,
        int parLevel, int reorderPoint, int reorderQuantity, string unitOfMeasure,
        bool isControlledSubstance);

    Task<bool> DispenseAsync(int quantity);
    Task ReceiveAsync(int quantity);
    Task SetParLevelAsync(int parLevel, int reorderPoint, int reorderQuantity);
    Task SetAutoReplenishAsync(bool enabled);
    Task RecordInventoryCountAsync(int actualCount);
    Task MarkReplenishmentPendingAsync();
    Task ClearReplenishmentPendingAsync();
}

/// <summary>
/// Ward Stock Index Grain — all drugs stocked at a ward.
/// Key: "WARD-STOCK-INDEX:{wardId}"
/// </summary>
public interface IWardStockIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Seeds a small demo data set through this grain. Owned here rather than in a
    /// controller so Blazor, WPF and the REST API all invoke one implementation — an
    /// internal UI must never call the WebServer to populate its own screen.
    /// </summary>
    Task SeedDemoDataAsync();

    Task<List<WardStockSummaryEntry>> GetAllItemsAsync();
    Task<List<WardStockSummaryEntry>> GetLowStockItemsAsync();
    Task AddOrUpdateAsync(WardStockSummaryEntry entry);
    Task RemoveAsync(string drugId);
}

/// <summary>
/// Ward Replenishment Log Grain — tracks replenishment requests per ward.
/// Key: "WARD-REPLENISH-LOG:{wardId}"
/// </summary>
public interface IWardReplenishmentLogGrain : IGrainWithStringKey
{
    Task<List<ReplenishmentRequest>> GetRequestsAsync();
    Task<List<ReplenishmentRequest>> GetPendingRequestsAsync();
    Task AddRequestAsync(ReplenishmentRequest request);
    Task FillRequestAsync(string requestId);
    Task CancelRequestAsync(string requestId);
}
