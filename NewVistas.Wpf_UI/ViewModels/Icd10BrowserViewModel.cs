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

public partial class Icd10BrowserViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private ObservableCollection<Icd10IndexEntry> _results = new();
    [ObservableProperty] private Icd10State? _selectedCode;
    [ObservableProperty] private Icd10IndexEntry? _selectedEntry;

    partial void OnSelectedEntryChanged(Icd10IndexEntry? value)
    {
        if (value is not null) _ = SelectCode(value);
    }

    [ObservableProperty] private bool _billableOnly;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public Icd10BrowserViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) return;
        IsLoading = true; Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
            List<Icd10IndexEntry> resultList = await indexGrain.SearchAsync(SearchTerm.Trim(), BillableOnly, 50);
            Results.Clear();
            foreach (Icd10IndexEntry r in resultList) Results.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectCode(Icd10IndexEntry entry)
    {
        try
        {
            var grain = _grains.GetGrain<IIcd10Grain>(entry.Code);
            SelectedCode = await grain.GetCodeAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
