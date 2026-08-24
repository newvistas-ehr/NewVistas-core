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

public partial class ServiceConnectedViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ServiceConnectedSummary> _conditions = new();

    // Record form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _condition = string.Empty;
    [ObservableProperty] private string _diagnosisCode = string.Empty;
    [ObservableProperty] private int? _disabilityPercentage;
    [ObservableProperty] private bool _isServiceConnected = true;
    [ObservableProperty] private DateTime? _effectiveDate;
    [ObservableProperty] private string _extremityAffected = string.Empty;

    public ServiceConnectedViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetServiceConnectedConditionsAsync();
        Conditions.Clear();
        foreach (var c in list) Conditions.Add(c);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private async Task RecordCondition()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(Condition)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordServiceConnectedConditionAsync(
                Condition,
                DiagnosisCode.Length > 0 ? DiagnosisCode : null,
                DisabilityPercentage,
                IsServiceConnected,
                EffectiveDate,
                ExtremityAffected.Length > 0 ? ExtremityAffected : null,
                null); // comments
            ShowRecordForm = false;
            Condition = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
