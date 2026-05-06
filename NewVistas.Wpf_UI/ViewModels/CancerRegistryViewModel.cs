// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class CancerRegistryViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<CancerRegistryReportIndexEntry> _reports = new();
    [ObservableProperty] private CancerRegistryReportState? _selectedReport;
    [ObservableProperty] private string? _actionMessage;

    public CancerRegistryViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        CancerRegistryReportState report = await workflow.GetCancerRegistryReportAsync(PatientId);
        // Load all reports from the index for the list view
        var index = Grains.GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");
        List<CancerRegistryReportIndexEntry> list = await index.GetAllReportsAsync();
        Reports.Clear();
        foreach (var r in list) Reports.Add(r);
        SelectedReport = null;
    }

    [RelayCommand]
    private async Task LoadAllReports()
    {
        IsLoading = true; Error = null;
        try
        {
            var index = Grains.GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");
            List<CancerRegistryReportIndexEntry> list = await index.GetAllReportsAsync();
            Reports.Clear();
            foreach (var r in list) Reports.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ViewReport(CancerRegistryReportIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(entry.PatientId);
            SelectedReport = await workflow.GetCancerRegistryReportAsync(entry.ReportId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
