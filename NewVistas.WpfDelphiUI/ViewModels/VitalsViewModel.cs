// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed partial class VitalsViewModel : ChartTabViewModelBase
{
    public ObservableCollection<VitalSummaryDto> Vitals { get; } = new();
    public ObservableCollection<VitalSummaryDto> HistoryVitals { get; } = new();

    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private DateTime _historyFrom = DateTime.Now.AddDays(-30);
    [ObservableProperty] private DateTime _historyTo = DateTime.Now;
    [ObservableProperty] private string _temperature = string.Empty;
    [ObservableProperty] private string _pulse = string.Empty;
    [ObservableProperty] private string _respiration = string.Empty;
    [ObservableProperty] private string _bloodPressure = string.Empty;
    [ObservableProperty] private string _height = string.Empty;
    [ObservableProperty] private string _weight = string.Empty;
    [ObservableProperty] private string _pain = string.Empty;
    [ObservableProperty] private string _pulseOximetry = string.Empty;

    public VitalsViewModel(ApiClient api, PatientContext context) : base(api, context) { }

    protected override async Task LoadAsync()
    {
        var items = await Api.GetVitalsAsync(PatientId);
        Vitals.Clear();
        foreach (var v in items) Vitals.Add(v);
    }

    protected override void ClearData()
    {
        Vitals.Clear();
        HistoryVitals.Clear();
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private void ToggleHistory() => ShowHistory = !ShowHistory;

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        ErrorText = string.Empty;
        try
        {
            var items = await Api.GetVitalHistoryAsync(PatientId, HistoryFrom, HistoryTo);
            HistoryVitals.Clear();
            foreach (var v in items) HistoryVitals.Add(v);
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task RecordVitalsAsync()
    {
        ErrorText = string.Empty;
        try
        {
            var vitals = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Temperature)) vitals["TEMPERATURE"] = Temperature;
            if (!string.IsNullOrWhiteSpace(Pulse)) vitals["PULSE"] = Pulse;
            if (!string.IsNullOrWhiteSpace(Respiration)) vitals["RESPIRATION"] = Respiration;
            if (!string.IsNullOrWhiteSpace(BloodPressure)) vitals["BLOOD PRESSURE"] = BloodPressure;
            if (!string.IsNullOrWhiteSpace(Height)) vitals["HEIGHT"] = Height;
            if (!string.IsNullOrWhiteSpace(Weight)) vitals["WEIGHT"] = Weight;
            if (!string.IsNullOrWhiteSpace(Pain)) vitals["PAIN"] = Pain;
            if (!string.IsNullOrWhiteSpace(PulseOximetry)) vitals["PULSE OXIMETRY"] = PulseOximetry;

            if (vitals.Count == 0) return;

            await Api.RecordVitalsAsync(PatientId, new { Vitals = vitals });
            Temperature = Pulse = Respiration = BloodPressure = string.Empty;
            Height = Weight = Pain = PulseOximetry = string.Empty;
            ShowRecordForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
