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

public partial class HealthFactorsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<HealthFactorSummary> _healthFactors = new();

    // Record form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _healthFactorName = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _levelSeverity = string.Empty;
    [ObservableProperty] private string _enteredByName = "Provider, Test";

    public string[] CategoryOptions { get; } = [
        "TOBACCO", "ALCOHOL", "DIET/NUTRITION", "EXERCISE",
        "SOCIAL", "OCCUPATIONAL", "FAMILY", "ENVIRONMENTAL", "OTHER"
    ];

    public string[] SeverityOptions { get; } = ["MINIMAL", "MODERATE", "HEAVY/SEVERE"];

    public HealthFactorsViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetHealthFactorsAsync();
        HealthFactors.Clear();
        foreach (var h in list) HealthFactors.Add(h);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private async Task RecordHealthFactor()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(HealthFactorName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordHealthFactorAsync(
                HealthFactorName,
                Category.Length > 0 ? Category : null,
                DateTime.UtcNow,
                LevelSeverity.Length > 0 ? LevelSeverity : null,
                null, // visitId
                null, null, // location
                null, EnteredByName, // enteredBy
                null); // comments
            ShowRecordForm = false;
            HealthFactorName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
