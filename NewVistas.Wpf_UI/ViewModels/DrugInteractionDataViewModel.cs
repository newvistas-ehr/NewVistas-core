// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class DrugInteractionDataViewModel : BasePatientViewModel
{
    [ObservableProperty] private DrugInteractionStatus? _datasetStatus;
    [ObservableProperty] private ObservableCollection<DrugInteractionPair> _allPairs = new();
    [ObservableProperty] private DrugInteractionPair? _selectedPair;
    [ObservableProperty] private bool? _cacheReady;
    [ObservableProperty] private string? _actionMessage;

    // Lookup fields
    [ObservableProperty] private string _lookupIen1 = string.Empty;
    [ObservableProperty] private string _lookupIen2 = string.Empty;

    public DrugInteractionDataViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var dataset = Grains.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
        var checker = Grains.GetGrain<IDrugInteractionCheckerGrain>("CHECKER");
        DatasetStatus = await dataset.GetStatusAsync();
        CacheReady = await checker.IsCacheReadyAsync();
    }

    [RelayCommand]
    private async Task LoadAllPairs()
    {
        IsLoading = true; Error = null;
        try
        {
            var dataset = Grains.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
            List<DrugInteractionPair> pairs = await dataset.GetAllInteractionsAsync();
            AllPairs.Clear();
            foreach (var p in pairs) AllPairs.Add(p);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LookupPair()
    {
        if (string.IsNullOrWhiteSpace(LookupIen1) || string.IsNullOrWhiteSpace(LookupIen2)) return;
        Error = null;
        try
        {
            var dataset = Grains.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
            SelectedPair = await dataset.GetInteractionAsync(LookupIen1.Trim(), LookupIen2.Trim());
            if (SelectedPair is null) Error = "No interaction found between these ingredients.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
