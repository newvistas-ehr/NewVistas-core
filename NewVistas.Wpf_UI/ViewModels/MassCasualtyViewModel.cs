// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class MassCasualtyViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<MassCasualtyIncidentIndexEntry> _incidents = new();
    [ObservableProperty] private ObservableCollection<MassCasualtyCasualtyIndexEntry> _casualties = new();
    [ObservableProperty] private string _incidentIdFilter = string.Empty;

    public MassCasualtyViewModel(OrleansGrainService grains) { _grains = grains; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true; Error = null; Incidents.Clear();
        try
        {
            var sp = _grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            IsFeatureEnabled = await sp.IsFeatureEnabledAsync("MASS_CASUALTY");
            if (!IsFeatureEnabled) return;
            var idx = _grains.GetGrain<IMassCasualtyIncidentIndexGrain>("MCI-IDX");
            foreach (var i in await idx.GetActiveAsync()) Incidents.Add(i);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task LoadCasualtiesAsync()
    {
        if (string.IsNullOrWhiteSpace(IncidentIdFilter)) return;
        IsLoading = true; Error = null; Casualties.Clear();
        try
        {
            var idx = _grains.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX");
            foreach (var c in await idx.GetByIncidentAsync(IncidentIdFilter.Trim())) Casualties.Add(c);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
