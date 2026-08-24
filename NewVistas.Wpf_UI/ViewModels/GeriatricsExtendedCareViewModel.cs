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

public partial class GeriatricsExtendedCareViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private ObservableCollection<GECAssessmentIndexEntry> _assessments = new();
    [ObservableProperty] private GECAssessmentIndexEntry? _selectedAssessment;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public GeriatricsExtendedCareViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var indexGrain = _grains.GetGrain<IGECAssessmentIndexGrain>($"GEC-ASSESS-IDX:{pid}");
            List<GECAssessmentIndexEntry> list = await indexGrain.GetAllAssessmentsAsync();
            Assessments.Clear();
            foreach (GECAssessmentIndexEntry a in list) Assessments.Add(a);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
