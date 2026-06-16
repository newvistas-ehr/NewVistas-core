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

public partial class AllergiesViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<AllergySummary> _allergies = new();

    // Record form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _allergen = string.Empty;
    [ObservableProperty] private string _allergenType = "DRUG";
    [ObservableProperty] private string _severity = "MODERATE";
    [ObservableProperty] private string _observedHistorical = "O";
    [ObservableProperty] private string _reactions = string.Empty;
    [ObservableProperty] private string _originatorName = "Provider, Test";

    public string[] AllergenTypes { get; } = ["DRUG", "FOOD", "OTHER"];
    public string[] SeverityOptions { get; } = ["MILD", "MODERATE", "SEVERE"];
    public string[] ObservedOptions { get; } = ["O", "H"];

    public AllergiesViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetAllergiesAsync();
        Allergies.Clear();
        foreach (var a in list) Allergies.Add(a);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private async Task RecordAllergy()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(Allergen)) return;
        IsLoading = true; Error = null;
        try
        {
            var reactionList = Reactions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordAllergyAsync(
                Allergen,
                AllergenType,
                null, // reactantId
                ObservedHistorical,
                reactionList.Count > 0 ? reactionList : null,
                Severity,
                null, // originatorId
                OriginatorName,
                null); // comments
            ShowRecordForm = false;
            Allergen = string.Empty;
            Reactions = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
