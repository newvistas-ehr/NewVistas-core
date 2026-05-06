// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ClinicalQualityMeasuresViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<CqmMeasureSummary> _measures = new();
    [ObservableProperty] private CqmReportState? _selectedReport;
    [ObservableProperty] private string _viewReportId = string.Empty;

    public ClinicalQualityMeasuresViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var index = Grains.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
        List<CqmMeasureSummary> list = await index.GetAllMeasuresAsync();
        Measures.Clear();
        foreach (var m in list) Measures.Add(m);
    }

    [RelayCommand]
    private async Task LoadActiveMeasures()
    {
        IsLoading = true; Error = null;
        try
        {
            var index = Grains.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
            List<CqmMeasureSummary> list = await index.GetActiveMeasuresAsync();
            Measures.Clear();
            foreach (var m in list) Measures.Add(m);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadReport()
    {
        if (string.IsNullOrWhiteSpace(ViewReportId)) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<ICqmReportGrain>(ViewReportId.Trim());
            SelectedReport = await grain.GetReportAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
