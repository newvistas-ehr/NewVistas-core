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

public partial class MasterPatientIndexViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    // Search
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private ObservableCollection<MpiSearchResult> _searchResults = new();

    // Match
    [ObservableProperty] private string _matchName = string.Empty;
    [ObservableProperty] private string _matchSsn = string.Empty;
    [ObservableProperty] private string _matchDob = string.Empty;
    [ObservableProperty] private string _matchSex = string.Empty;
    [ObservableProperty] private ObservableCollection<MpiMatchCandidate> _matchCandidates = new();

    public MasterPatientIndexViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) { Error = "Enter a search query"; return; }
        IsLoading = true;
        Error = null;
        try
        {
            var searchGrain = _grains.GetGrain<IMpiSearchGrain>("MPI-INDEX");
            List<MpiSearchResult> list = await searchGrain.SearchAsync(SearchQuery, 50);
            SearchResults.Clear();
            foreach (MpiSearchResult r in list) SearchResults.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task FindMatchAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var matchGrain = _grains.GetGrain<IMpiMatchGrain>("MPI-MATCHER");
            DateTime? dob = DateTime.TryParse(MatchDob, out DateTime d) ? d : null;
            string? sex = string.IsNullOrEmpty(MatchSex) ? null : MatchSex;
            List<MpiMatchCandidate> list = await matchGrain.FindCandidatesAsync(
                MatchName, MatchSsn, dob, sex, 0.0);
            MatchCandidates.Clear();
            foreach (MpiMatchCandidate c in list) MatchCandidates.Add(c);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            var searchGrain = _grains.GetGrain<IMpiSearchGrain>("MPI-INDEX");
            await searchGrain.SeedDemoDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
