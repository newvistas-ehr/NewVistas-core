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

public partial class SchedulingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<AppointmentEntry> _appointments = new();
    [ObservableProperty] private ObservableCollection<ClinicEntry> _clinics = new();
    [ObservableProperty] private AppointmentState? _selectedAppointment;

    // Schedule form
    [ObservableProperty] private bool _showScheduleForm;
    [ObservableProperty] private string _clinicId = string.Empty;
    [ObservableProperty] private string _clinicName = string.Empty;
    [ObservableProperty] private DateTime _appointmentDateTime = DateTime.Today.AddDays(7);
    [ObservableProperty] private int _durationMinutes = 30;
    [ObservableProperty] private string _purpose = "REGULAR";
    [ObservableProperty] private string _providerName = "Provider, Test";

    public string[] PurposeOptions { get; } = ["REGULAR", "FOLLOW-UP", "URGENT", "POST-OP", "PROCEDURE", "CONSULTATION"];

    public SchedulingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetAllAppointmentsAsync(50);
        Appointments.Clear();
        foreach (var a in list) Appointments.Add(a);

        var clinicList = await workflow.GetClinicListAsync();
        Clinics.Clear();
        foreach (var c in clinicList) Clinics.Add(c);
    }

    [RelayCommand]
    private void ToggleScheduleForm() => ShowScheduleForm = !ShowScheduleForm;

    [RelayCommand]
    private async Task ScheduleAppointment()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ClinicName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.ScheduleAppointmentAsync(
                ClinicId.Length > 0 ? ClinicId : Guid.NewGuid().ToString(),
                ClinicName,
                AppointmentDateTime,
                DurationMinutes,
                null, // providerId
                ProviderName,
                Purpose,
                null); // appointmentType
            ShowScheduleForm = false;
            ClinicName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CheckIn()
    {
        if (SelectedAppointment is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CheckInAsync(SelectedAppointment.AppointmentId, null);
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CancelAppointment()
    {
        if (SelectedAppointment is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CancelAppointmentAsync(SelectedAppointment.AppointmentId);
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
