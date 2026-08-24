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

public partial class InfectionControlViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<HAICaseSummary> _cases = new();
    [ObservableProperty] private ObservableCollection<OutbreakSummary> _outbreaks = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public InfectionControlViewModel(OrleansGrainService grains)
    {
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
