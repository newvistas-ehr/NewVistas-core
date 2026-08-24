// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class WardStockViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _wardId = string.Empty;
    [ObservableProperty] private ObservableCollection<WardStockSummaryEntry> _stockItems = new();
    [ObservableProperty] private ObservableCollection<WardStockSummaryEntry> _lowStockItems = new();
    [ObservableProperty] private ObservableCollection<ReplenishmentRequest> _replenishmentLog = new();

    public WardStockViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(WardId)) { Error = "Enter a Ward ID"; return; }
        IsLoading = true;
        Error = null;
        try
        {
            string wid = WardId.Trim();
            var indexGrain = _grains.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{wid}");
            List<WardStockSummaryEntry> items = await indexGrain.GetAllItemsAsync();
            StockItems.Clear();
            foreach (WardStockSummaryEntry i in items) StockItems.Add(i);

            List<WardStockSummaryEntry> low = await indexGrain.GetLowStockItemsAsync();
            LowStockItems.Clear();
            foreach (WardStockSummaryEntry l in low) LowStockItems.Add(l);

            var logGrain = _grains.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{wid}");
            List<ReplenishmentRequest> log = await logGrain.GetRequestsAsync();
            ReplenishmentLog.Clear();
            foreach (ReplenishmentRequest r in log) ReplenishmentLog.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            // The grain owns the demo seed, so this is one grain call - no HTTP.
            await _grains.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{WardId}").SeedDemoDataAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
