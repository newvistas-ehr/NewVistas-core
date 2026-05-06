// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ClinicalCaseRegistriesViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<CCREntrySummary> _entries = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ClinicalCaseRegistriesViewModel(ApiClient api, OrleansGrainService grains)
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
            var siteIndex = _grains.GetGrain<IClinicalRegistrySiteIndexGrain>("CCR-SITE-IDX:DEFAULT");
            List<CCREntrySummary> list = await siteIndex.GetRecentEnrollmentsAsync(50);
            Entries.Clear();
            foreach (CCREntrySummary e in list) Entries.Add(e);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
