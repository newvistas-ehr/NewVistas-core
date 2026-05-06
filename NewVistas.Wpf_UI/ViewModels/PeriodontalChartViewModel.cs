// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class PeriodontalChartViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<PeriodontalChartIndexEntry> _charts = new();

    public PeriodontalChartViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null; Charts.Clear();
        var sp = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await sp.IsFeatureEnabledAsync("PERIODONTAL_CHARTING");
        if (!IsFeatureEnabled) return;

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        foreach (var c in await workflow.GetPeriodontalChartsAsync()) Charts.Add(c);
    }

    [RelayCommand]
    public async Task CreateChartAsync()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreatePeriodontalChartAsync("PROVIDER-CURRENT", "Provider, Test", null);
            SuccessMessage = "Periodontal chart created.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
