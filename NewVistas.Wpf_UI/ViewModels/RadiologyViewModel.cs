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

public partial class RadiologyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<RadiologySummary> _studies = new();
    [ObservableProperty] private RadiologyState? _selectedStudy;

    // Order form
    [ObservableProperty] private bool _showOrderForm;
    [ObservableProperty] private string _procedureName = string.Empty;
    [ObservableProperty] private string _imagingType = "GENERAL RADIOLOGY";
    [ObservableProperty] private string _urgency = "ROUTINE";
    [ObservableProperty] private string _clinicalHistory = string.Empty;
    [ObservableProperty] private string _requestingProviderName = "Provider, Test";

    public string[] ImagingTypes { get; } = [
        "GENERAL RADIOLOGY", "CT SCAN", "MRI", "ULTRASOUND",
        "NUCLEAR MEDICINE", "PET SCAN", "MAMMOGRAPHY", "FLUOROSCOPY"
    ];
    public string[] UrgencyOptions { get; } = ["ROUTINE", "URGENT", "STAT", "TODAY"];

    public RadiologyViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetRadiologyStudiesAsync(50);
        Studies.Clear();
        foreach (var s in list) Studies.Add(s);
    }

    [RelayCommand]
    private async Task SelectStudy(RadiologySummary s)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedStudy = await workflow.GetRadiologyStudyAsync(s.RadiologyId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleOrderForm() => ShowOrderForm = !ShowOrderForm;

    [RelayCommand]
    private async Task OrderStudy()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ProcedureName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.OrderRadiologyStudyAsync(
                ProcedureName,
                null, null, // procedureId, cptCode
                ImagingType,
                null, RequestingProviderName, // requestingProvider
                Urgency,
                ClinicalHistory.Length > 0 ? ClinicalHistory : null,
                null, // reasonForStudy
                null, null, null); // orderId, location
            ShowOrderForm = false;
            ProcedureName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
