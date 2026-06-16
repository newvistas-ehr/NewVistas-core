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

public partial class AdtViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<AdtSummary> _movements = new();
    [ObservableProperty] private AdtSummary? _selectedMovement;
    [ObservableProperty] private int _selectedTab; // 0=Movements, 1=Census

    // Admit form
    [ObservableProperty] private bool _showAdmitForm;
    [ObservableProperty] private string _wardLocationName = string.Empty;
    [ObservableProperty] private string _roomBed = string.Empty;
    [ObservableProperty] private string _treatingSpecialty = string.Empty;
    [ObservableProperty] private string _attendingPhysicianName = "Physician, Test";
    [ObservableProperty] private string _admissionDiagnosis = string.Empty;

    // Discharge form
    [ObservableProperty] private bool _showDischargeForm;
    [ObservableProperty] private string _dischargeDiagnosis = string.Empty;
    [ObservableProperty] private string _disposition = "HOME";

    public string[] DispositionOptions { get; } = [
        "HOME", "SNF", "REHAB", "LTAC", "HOSPICE", "AMA", "EXPIRED", "TRANSFERRED"
    ];

    public AdtViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetAdtMovementsAsync();
        Movements.Clear();
        foreach (var m in list) Movements.Add(m);
    }

    [RelayCommand]
    private void ToggleAdmitForm() => ShowAdmitForm = !ShowAdmitForm;

    [RelayCommand]
    private async Task RecordAdmission()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordAdmissionAsync(
                DateTime.UtcNow,
                null,
                WardLocationName.Length > 0 ? WardLocationName : null,
                RoomBed.Length > 0 ? RoomBed : null,
                TreatingSpecialty.Length > 0 ? TreatingSpecialty : null,
                null,
                AttendingPhysicianName,
                AdmissionDiagnosis.Length > 0 ? AdmissionDiagnosis : null,
                null);
            ShowAdmitForm = false;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RecordDischarge()
    {
        if (SelectedMovement is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordDischargeAsync(
                SelectedMovement.MovementId,
                DateTime.UtcNow,
                DischargeDiagnosis.Length > 0 ? DischargeDiagnosis : null,
                Disposition,
                null);
            ShowDischargeForm = false;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
