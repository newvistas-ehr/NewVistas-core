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

public partial class LexiconViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    // Search
    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private string _searchSystem = string.Empty;
    [ObservableProperty] private int _maxResults = 50;
    [ObservableProperty] private ObservableCollection<LexiconIndexEntry> _searchResults = new();

    // Lookup
    [ObservableProperty] private string _lookupSystem = string.Empty;
    [ObservableProperty] private string _lookupCode = string.Empty;
    [ObservableProperty] private LexiconIndexEntry? _lookupResult;

    public string[] Systems { get; } = [string.Empty, "SNOMED", "ICD10", "CPT", "LOINC"];

    public LexiconViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) { Error = "Enter a search term"; return; }
        IsLoading = true;
        Error = null;
        try
        {
            var grain = _grains.GetGrain<ILexiconSearchGrain>("LEX-INDEX");
            string? system = string.IsNullOrEmpty(SearchSystem) ? null : SearchSystem;
            List<LexiconIndexEntry> list = await grain.SearchAsync(SearchTerm, system, MaxResults);
            SearchResults.Clear();
            foreach (LexiconIndexEntry r in list) SearchResults.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LookupAsync()
    {
        if (string.IsNullOrWhiteSpace(LookupSystem) || string.IsNullOrWhiteSpace(LookupCode))
        {
            Error = "Enter both system and code";
            return;
        }
        try
        {
            var grain = _grains.GetGrain<ILexiconSearchGrain>("LEX-INDEX");
            LookupResult = await grain.LookupCodeAsync(LookupCode, LookupSystem);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            var grain = _grains.GetGrain<ILexiconSearchGrain>("LEX-INDEX");
            await grain.SeedDemoDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
