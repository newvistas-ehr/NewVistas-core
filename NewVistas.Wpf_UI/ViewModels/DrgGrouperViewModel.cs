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

public partial class DrgGrouperViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<DrgAssignmentEntry> _assignments = new();
    [ObservableProperty] private DrgAssignmentEntry? _selectedAssignment;

    public DrgGrouperViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IDrgIndexGrain>("DRG-INDEX:DEFAULT");
            List<DrgAssignmentEntry> list = await indexGrain.GetAllAssignmentsAsync();
            Assignments.Clear();
            foreach (DrgAssignmentEntry a in list) Assignments.Add(a);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ReviewAsync()
    {
        if (SelectedAssignment == null) return;
        try
        {
            var grain = _grains.GetGrain<IDrgAssignmentGrain>($"DRG:{SelectedAssignment.AdmissionId}");
            await grain.ReviewAsync("ADMIN");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task FinalizeAsync()
    {
        if (SelectedAssignment == null) return;
        try
        {
            var grain = _grains.GetGrain<IDrgAssignmentGrain>($"DRG:{SelectedAssignment.AdmissionId}");
            await grain.FinalizeAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            // The index grain owns the demo seed, so this is one grain call.
            // (It also now seeds the SAME facility index this screen reads —
            // the old endpoint defaulted to "MAIN" while this view reads "DEFAULT",
            // so the button appeared to do nothing.)
            await _grains.GetGrain<IDrgIndexGrain>("DRG-INDEX:DEFAULT").SeedDemoDataAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
