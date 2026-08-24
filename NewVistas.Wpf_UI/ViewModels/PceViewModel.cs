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

public partial class PceViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<PceVisitEntry> _encounters = new();
    [ObservableProperty] private VisitState? _selectedEncounter;

    /// <summary>Grid selection; loads the full visit into <see cref="SelectedEncounter"/>.</summary>
    [ObservableProperty] private PceVisitEntry? _selectedVisit;

    partial void OnSelectedVisitChanged(PceVisitEntry? value)
    {
        // Actions gate on the detail object; clear it before the async fetch so they can never target the previously selected record.
        SelectedEncounter = null;
        if (value is not null) _ = SelectEncounter(value);
    }

    // Create form
    [ObservableProperty] private bool _showCreateForm;
    [ObservableProperty] private string _serviceCategory = "AMBULATORY";
    [ObservableProperty] private string _visitType = string.Empty;
    [ObservableProperty] private string _locationName = string.Empty;
    [ObservableProperty] private string _stopCode = string.Empty;
    [ObservableProperty] private string _primaryProviderName = "Provider, Test";
    [ObservableProperty] private DateTime _visitDateTime = DateTime.Today;

    public string[] ServiceCategories { get; } = [
        "AMBULATORY", "INPATIENT", "TELEPHONE", "TELEHEALTH",
        "DAY TREATMENT", "HOME VISIT", "NURSING HOME"
    ];

    public string[] VisitTypeOptions { get; } = ["NEW", "ESTABLISHED"];

    public PceViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetEncounterListAsync(50);
        Encounters.Clear();
        foreach (var e in list) Encounters.Add(e);
    }

    [RelayCommand]
    private async Task SelectEncounter(PceVisitEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedEncounter = await workflow.GetEncounterAsync(entry.VisitId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleCreateForm() => ShowCreateForm = !ShowCreateForm;

    [RelayCommand]
    private async Task CreateEncounter()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateEncounterAsync(
                VisitDateTime,
                ServiceCategory,
                null, // locationId
                LocationName.Length > 0 ? LocationName : null,
                VisitType.Length > 0 ? VisitType : null,
                StopCode.Length > 0 ? StopCode : null,
                null, // primaryProviderId
                PrimaryProviderName,
                null, // linkedAppointmentId
                null); // comments
            ShowCreateForm = false;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CheckOutEncounter()
    {
        if (SelectedEncounter is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CheckOutEncounterAsync(SelectedEncounter.VisitId, DateTime.UtcNow);
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
