// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class VitalsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<VitalSummary> _vitals = new();

    // History
    [ObservableProperty] private ObservableCollection<VitalSummary> _historyVitals = new();
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private DateTime _historyFrom = DateTime.Now.AddDays(-30);
    [ObservableProperty] private DateTime _historyTo = DateTime.Now;

    // Record vitals form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _temperature = string.Empty;
    [ObservableProperty] private string _pulse = string.Empty;
    [ObservableProperty] private string _respiration = string.Empty;
    [ObservableProperty] private string _bloodPressureSystolic = string.Empty;
    [ObservableProperty] private string _bloodPressureDiastolic = string.Empty;
    [ObservableProperty] private string _weight = string.Empty;
    [ObservableProperty] private string _height = string.Empty;
    [ObservableProperty] private string _oxygenSaturation = string.Empty;
    [ObservableProperty] private string _pain = string.Empty;
    [ObservableProperty] private string _enteredByName = "Nurse, Test";

    public VitalsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetLatestVitalsAsync();
        Vitals.Clear();
        foreach (var v in list) Vitals.Add(v);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private void ToggleHistory() => ShowHistory = !ShowHistory;

    [RelayCommand]
    private async Task LoadHistory()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            var list = await workflow.GetVitalHistoryAsync(HistoryFrom, HistoryTo, 100);
            HistoryVitals.Clear();
            foreach (var v in list) HistoryVitals.Add(v);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RecordVitals()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var vitalsDict = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Temperature)) vitalsDict["TEMPERATURE"] = Temperature;
            if (!string.IsNullOrWhiteSpace(Pulse)) vitalsDict["PULSE"] = Pulse;
            if (!string.IsNullOrWhiteSpace(Respiration)) vitalsDict["RESPIRATION"] = Respiration;
            if (!string.IsNullOrWhiteSpace(BloodPressureSystolic) && !string.IsNullOrWhiteSpace(BloodPressureDiastolic))
                vitalsDict["BLOOD PRESSURE"] = $"{BloodPressureSystolic}/{BloodPressureDiastolic}";
            if (!string.IsNullOrWhiteSpace(Weight)) vitalsDict["WEIGHT"] = Weight;
            if (!string.IsNullOrWhiteSpace(Height)) vitalsDict["HEIGHT"] = Height;
            if (!string.IsNullOrWhiteSpace(OxygenSaturation)) vitalsDict["PULSE OXIMETRY"] = OxygenSaturation;
            if (!string.IsNullOrWhiteSpace(Pain)) vitalsDict["PAIN"] = Pain;

            if (vitalsDict.Count == 0) { Error = "Enter at least one vital sign."; return; }

            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordVitalsAsync(
                null, null, // location
                null, EnteredByName, // enteredBy
                DateTime.UtcNow,
                vitalsDict,
                null); // qualifiers

            ShowRecordForm = false;
            Temperature = Pulse = Respiration = BloodPressureSystolic = BloodPressureDiastolic =
                Weight = Height = OxygenSaturation = Pain = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
