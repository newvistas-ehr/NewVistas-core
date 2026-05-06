// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class VoluntaryServiceViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<VolunteerIndexEntry> _volunteers = new();
    [ObservableProperty] private VolunteerIndexEntry? _selectedVolunteer;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public VoluntaryServiceViewModel(ApiClient api, OrleansGrainService grains)
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
            var indexGrain = _grains.GetGrain<IVolunteerIndexGrain>("VS-INDEX");
            List<VolunteerIndexEntry> list = string.IsNullOrWhiteSpace(SearchText)
                ? await indexGrain.GetAllAsync()
                : await indexGrain.SearchAsync(SearchText.Trim());
            Volunteers.Clear();
            foreach (VolunteerIndexEntry v in list) Volunteers.Add(v);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
