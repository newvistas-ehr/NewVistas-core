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

public partial class ControlledSubstancesViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _locationId = "PHARMACY-MAIN";
    [ObservableProperty] private ObservableCollection<CSInspectionSummaryEntry> _inspections = new();
    [ObservableProperty] private ObservableCollection<CSDispenseSummaryEntry> _dispenseRecords = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ControlledSubstancesViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(LocationId)) return;
        IsLoading = true; Error = null;
        try
        {
            string loc = LocationId.Trim();
            var inspLogGrain = _grains.GetGrain<ICSInspectionLogGrain>($"CS-INSPECT-LOG:{loc}");
            List<CSInspectionSummaryEntry> insp = await inspLogGrain.GetAllInspectionsAsync();
            Inspections.Clear();
            foreach (CSInspectionSummaryEntry i in insp) Inspections.Add(i);

            var dispLogGrain = _grains.GetGrain<ICSDispenseLogGrain>($"CS-DISPENSE-LOG:{loc}");
            List<CSDispenseSummaryEntry> disp = await dispLogGrain.GetAllRecordsAsync();
            DispenseRecords.Clear();
            foreach (CSDispenseSummaryEntry d in disp) DispenseRecords.Add(d);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
