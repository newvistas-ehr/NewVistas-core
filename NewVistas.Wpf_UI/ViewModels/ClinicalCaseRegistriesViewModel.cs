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

public partial class ClinicalCaseRegistriesViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<CCREntrySummary> _entries = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ClinicalCaseRegistriesViewModel(OrleansGrainService grains)
    {
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
