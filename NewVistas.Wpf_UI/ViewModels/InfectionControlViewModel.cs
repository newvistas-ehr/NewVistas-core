// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class InfectionControlViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<HAICaseSummary> _cases = new();
    [ObservableProperty] private ObservableCollection<OutbreakSummary> _outbreaks = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public InfectionControlViewModel(ApiClient api, OrleansGrainService grains)
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
            var caseIndex = _grains.GetGrain<IHAICaseIndexGrain>("HAI-CASE-IDX");
            List<HAICaseSummary> cases = await caseIndex.GetAllCasesAsync();
            Cases.Clear();
            foreach (HAICaseSummary c in cases) Cases.Add(c);

            var outbreakIndex = _grains.GetGrain<IOutbreakIndexGrain>("HAI-OUTBREAK-IDX");
            List<OutbreakSummary> outbreaks = await outbreakIndex.GetAllOutbreaksAsync();
            Outbreaks.Clear();
            foreach (OutbreakSummary o in outbreaks) Outbreaks.Add(o);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
