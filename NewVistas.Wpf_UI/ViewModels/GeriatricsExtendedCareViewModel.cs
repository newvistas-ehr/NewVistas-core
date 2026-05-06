// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class GeriatricsExtendedCareViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private ObservableCollection<GECAssessmentIndexEntry> _assessments = new();
    [ObservableProperty] private GECAssessmentIndexEntry? _selectedAssessment;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public GeriatricsExtendedCareViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
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
