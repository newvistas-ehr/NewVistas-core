// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// Reports tab — radiology reports (per-patient) plus a facility-wide GPRA
/// section listing recent population-health reports. The GPRA section is
/// independent of the selected patient (the report is facility-scoped).
/// </summary>
public sealed partial class ReportsViewModel : ChartTabViewModelBase
{
    public ObservableCollection<RadiologyReportDto>     Reports     { get; } = new();
    public ObservableCollection<GpraReportIndexEntryDto> GpraReports { get; } = new();
    public ObservableCollection<GpraIndicatorResultDto>  GpraIndicators { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedGpraReportHeader))]
    private GpraReportIndexEntryDto? _selectedGpraReport;

    public string SelectedGpraReportHeader => SelectedGpraReport switch
    {
        null => "No GPRA report selected",
        var r => $"{r.FacilityName} — FY{r.FiscalYear} {ReportingPeriodLabel(r.ReportingPeriod)} " +
                 $"({StatusLabel(r.Status)}, {r.IndicatorCount} indicators, " +
                 $"AUP={r.ActiveUserPopulation:N0})",
    };

    public ReportsViewModel(ChartDataService data, PatientContext context) : base(data, context) { }

    protected override async Task LoadAsync()
    {
        var radiology = Data.GetRadiologyReportsAsync(PatientId);
        var gpra      = SafeGetGpraReportsAsync();

        await Task.WhenAll(radiology, gpra);

        Reports.Clear();
        foreach (var r in await radiology) Reports.Add(r);

        GpraReports.Clear();
        foreach (var g in await gpra) GpraReports.Add(g);
    }

    private async Task<List<GpraReportIndexEntryDto>> SafeGetGpraReportsAsync()
    {
        try { return await Data.GetGpraReportsAsync(); }
        catch { return []; }
    }

    protected override void ClearData()
    {
        Reports.Clear();
        GpraReports.Clear();
        GpraIndicators.Clear();
        SelectedGpraReport = null;
    }

    [RelayCommand]
    private async Task LoadGpraIndicatorsAsync()
    {
        if (SelectedGpraReport is null)
        {
            GpraIndicators.Clear();
            return;
        }
        ErrorText = string.Empty;
        try
        {
            GpraReportDto? full = await Data.GetGpraReportAsync(SelectedGpraReport.ReportId);
            GpraIndicators.Clear();
            if (full is not null)
                foreach (var ind in full.Indicators) GpraIndicators.Add(ind);
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    partial void OnSelectedGpraReportChanged(GpraReportIndexEntryDto? value)
        => _ = LoadGpraIndicatorsAsync();

    public static string ReportingPeriodLabel(int code) => code switch
    {
        0 => "Full FY", 1 => "Q1", 2 => "Q2", 3 => "Q3", 4 => "Q4", _ => $"({code})",
    };

    public static string StatusLabel(int code) => code switch
    {
        0 => "Draft", 1 => "Evaluating", 2 => "Completed", 3 => "Error", _ => $"({code})",
    };

    public static string CategoryLabel(int code) => code switch
    {
        0 => "Diabetes", 1 => "CV", 2 => "Women's Health", 3 => "Immunizations",
        4 => "Behavioral Health", 5 => "Preventive", 6 => "Asthma",
        7 => "Child Health", 8 => "Oral Health", 9 => "OB/GYN",
        _ => $"({code})",
    };
}
