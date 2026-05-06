// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class QualityManagementViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<QMIncidentIndexEntry> _incidents = new();
    [ObservableProperty] private QMIncidentIndexEntry? _selectedIncident;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public QualityManagementViewModel(ApiClient api, OrleansGrainService grains)
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
            var indexGrain = _grains.GetGrain<IQMIncidentIndexGrain>("QM-INCIDENT-IDX");
            List<QMIncidentIndexEntry> list = await indexGrain.GetAllIncidentsAsync();
            Incidents.Clear();
            foreach (QMIncidentIndexEntry i in list) Incidents.Add(i);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
