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

public partial class BcmaViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<MarEntry> _marEntries = new();
    [ObservableProperty] private ObservableCollection<BcmaSummary> _administrations = new();
    [ObservableProperty] private int _selectedTab; // 0=MAR, 1=History

    // Administer form
    [ObservableProperty] private bool _showAdminForm;
    [ObservableProperty] private string _drugName = string.Empty;
    [ObservableProperty] private string _dosage = string.Empty;
    [ObservableProperty] private string _route = "PO";
    [ObservableProperty] private string _actionStatus = "GIVEN";
    [ObservableProperty] private string _administeredByName = "Nurse, Test";

    public string[] ActionStatuses { get; } = ["GIVEN", "NOT GIVEN", "HELD", "REFUSED", "REFUSED-PATIENT EDUCATION"];
    public string[] RouteOptions { get; } = ["PO", "IV", "IM", "SC", "SL", "TOP", "INH", "PR", "ID"];

    public BcmaViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var mar = await workflow.GetPatientMARAsync();
        MarEntries.Clear();
        foreach (var m in mar) MarEntries.Add(m);

        var hist = await workflow.GetMedicationAdministrationsAsync(50);
        Administrations.Clear();
        foreach (var a in hist) Administrations.Add(a);
    }

    [RelayCommand]
    private void ToggleAdminForm() => ShowAdminForm = !ShowAdminForm;

    [RelayCommand]
    private async Task AdministerMedication()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(DrugName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordMedicationAdministrationAsync(
                DrugName,
                null, // drugId
                Dosage.Length > 0 ? Dosage : null,
                Route,
                ActionStatus,
                null, // scheduledDateTime
                DateTime.UtcNow,
                null, // administeredById
                AdministeredByName,
                null, // injectionSite
                null, // prescriptionId
                null, // orderId
                null); // comments
            ShowAdminForm = false;
            DrugName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
