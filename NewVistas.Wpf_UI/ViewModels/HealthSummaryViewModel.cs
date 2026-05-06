// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class HealthSummaryViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<HealthSummaryIndexEntry> _summaries = new();
    [ObservableProperty] private HealthSummaryIndexEntry? _selectedSummary;
    [ObservableProperty] private HealthSummaryState? _summaryDetail;

    public HealthSummaryViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetHealthSummaryListAsync();
        Summaries.Clear();
        foreach (var s in list) Summaries.Add(s);
    }

    [RelayCommand]
    private async Task SelectSummary(HealthSummaryIndexEntry entry)
    {
        SelectedSummary = entry;
        SummaryDetail = null;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SummaryDetail = await workflow.GetHealthSummaryAsync(entry.ReportId);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
