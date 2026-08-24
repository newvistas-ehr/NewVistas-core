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

public partial class ImmunizationsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ImmunizationSummary> _immunizations = new();

    // Record form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _immunizationName = string.Empty;
    [ObservableProperty] private string _cvxCode = string.Empty;
    [ObservableProperty] private DateTime _eventDateTime = DateTime.Today;
    [ObservableProperty] private string _series = string.Empty;
    [ObservableProperty] private string _lotNumber = string.Empty;
    [ObservableProperty] private string _route = "IM";
    [ObservableProperty] private string _dose = string.Empty;
    [ObservableProperty] private string _site = string.Empty;
    [ObservableProperty] private string _administeredByName = "Nurse, Test";

    public string[] RouteOptions { get; } = ["IM", "SC", "ID", "PO", "IN", "IV"];
    public string[] SiteOptions { get; } =
        ["LEFT DELTOID", "RIGHT DELTOID", "LEFT THIGH", "RIGHT THIGH", "LEFT ARM", "RIGHT ARM"];

    public ImmunizationsViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetImmunizationsAsync();
        Immunizations.Clear();
        foreach (var i in list) Immunizations.Add(i);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private async Task RecordImmunization()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ImmunizationName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordImmunizationAsync(
                ImmunizationName,
                CvxCode.Length > 0 ? CvxCode : null,
                EventDateTime,
                Series.Length > 0 ? Series : null,
                LotNumber.Length > 0 ? LotNumber : null,
                null, // manufacturer
                null, AdministeredByName, // administeredBy
                Site.Length > 0 ? Site : null, // administrationSite
                Route,
                Dose.Length > 0 ? Dose : null,
                null, null, // location
                null); // comments
            ShowRecordForm = false;
            ImmunizationName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
