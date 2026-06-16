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

public partial class EngineeringViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<WorkOrderIndexEntry> _workOrders = new();
    [ObservableProperty] private WorkOrderIndexEntry? _selectedWorkOrder;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public EngineeringViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IEngineeringWorkOrderIndexGrain>("ENG-WO-IDX");
            List<WorkOrderIndexEntry> list = await indexGrain.GetActiveAsync(200);
            WorkOrders.Clear();
            foreach (WorkOrderIndexEntry w in list) WorkOrders.Add(w);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
