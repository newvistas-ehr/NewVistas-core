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

public partial class IfcapViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<ControlPointIndexEntry> _controlPoints = new();
    [ObservableProperty] private ObservableCollection<PurchaseOrderIndexEntry> _purchaseOrders = new();
    [ObservableProperty] private ObservableCollection<IfcapVendorIndexEntry> _vendors = new();
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private int _fiscalYear = DateTime.Now.Year;

    public IfcapViewModel(OrleansGrainService grains)
    {
        _grains = grains;
        _ = LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var cpIndex = _grains.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            List<ControlPointIndexEntry> cps = await cpIndex.GetByFiscalYearAsync(FiscalYear);
            ControlPoints.Clear();
            foreach (ControlPointIndexEntry cp in cps) ControlPoints.Add(cp);

            var poIndex = _grains.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            List<PurchaseOrderIndexEntry> pos = await poIndex.GetAllAsync();
            PurchaseOrders.Clear();
            foreach (PurchaseOrderIndexEntry po in pos) PurchaseOrders.Add(po);

            var vendorIndex = _grains.GetGrain<IIfcapVendorIndexGrain>("IFCAP-VENDOR-IDX");
            List<IfcapVendorIndexEntry> vendors = await vendorIndex.GetAllAsync();
            Vendors.Clear();
            foreach (IfcapVendorIndexEntry v in vendors) Vendors.Add(v);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadControlPoints()
    {
        IsLoading = true; Error = null;
        try
        {
            var cpIndex = _grains.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            List<ControlPointIndexEntry> list = await cpIndex.GetByFiscalYearAsync(FiscalYear);
            ControlPoints.Clear();
            foreach (ControlPointIndexEntry cp in list) ControlPoints.Add(cp);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAllAsync();
    }
}
