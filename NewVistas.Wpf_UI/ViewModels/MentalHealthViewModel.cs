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

public partial class MentalHealthViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<MentalHealthSummary> _screens = new();

    // Record form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _instrumentName = "PHQ-9";
    [ObservableProperty] private decimal? _totalScore;
    [ObservableProperty] private string _scoreInterpretation = string.Empty;
    [ObservableProperty] private bool? _isPositiveScreen;
    [ObservableProperty] private string _administeredByName = "Provider, Test";

    public string[] InstrumentOptions { get; } = [
        "PHQ-9", "PHQ-2", "GAD-7", "PC-PTSD-5", "AUDIT-C", "DAST-10",
        "C-SSRS", "MDQ", "CAGE", "CIWA-Ar"
    ];

    public MentalHealthViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetMentalHealthScreensAsync();
        Screens.Clear();
        foreach (var s in list) Screens.Add(s);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private async Task RecordScreen()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordMentalHealthScreenAsync(
                InstrumentName,
                DateTime.UtcNow,
                TotalScore,
                ScoreInterpretation.Length > 0 ? ScoreInterpretation : null,
                IsPositiveScreen,
                null, // responses
                null, // administeredById
                AdministeredByName,
                null, // locationId
                null, // locationName
                null); // comments
            ShowRecordForm = false;
            TotalScore = null;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
