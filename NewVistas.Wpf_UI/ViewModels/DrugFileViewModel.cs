// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class DrugFileViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private ObservableCollection<DrugIndexEntry> _results = new();
    [ObservableProperty] private DrugState? _selectedDrug;
    [ObservableProperty] private ObservableCollection<OrderableItemIndexEntry> _orderableItems = new();
    [ObservableProperty] private int _selectedTab; // 0=Drugs, 1=Orderable Items
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public DrugFileViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task SearchDrugs()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) return;
        IsLoading = true; Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IDrugIndexGrain>("DRUG-INDEX");
            List<DrugIndexEntry> resultList = await indexGrain.SearchAsync(SearchTerm.Trim(), null, true, 50);
            Results.Clear();
            foreach (DrugIndexEntry r in resultList) Results.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectDrug(DrugIndexEntry entry)
    {
        try
        {
            var drugGrain = _grains.GetGrain<IDrugGrain>(entry.Ien);
            SelectedDrug = await drugGrain.GetDrugAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadOrderableItems()
    {
        IsLoading = true; Error = null;
        try
        {
            var list = await _api.GetOrderableItemsAsync(100);
            OrderableItems.Clear();
            foreach (OrderableItemIndexEntry o in list) OrderableItems.Add(o);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
