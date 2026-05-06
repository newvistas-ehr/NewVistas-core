// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class PolytraumaTBIViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<PolytraumaRegistrySummaryEntry> _patients = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public PolytraumaTBIViewModel(ApiClient api, OrleansGrainService grains)
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
            var indexGrain = _grains.GetGrain<IPolytraumaRegistryIndexGrain>("PT-REGISTRY-IDX");
            List<PolytraumaRegistrySummaryEntry> list = await indexGrain.GetActivePatientAsync();
            Patients.Clear();
            foreach (PolytraumaRegistrySummaryEntry p in list) Patients.Add(p);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
