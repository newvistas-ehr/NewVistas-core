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

public partial class AppointmentWaitListViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<AppointmentWaitListIndexEntry> _entries = new();
    [ObservableProperty] private bool _showNewForm;

    // Form fields
    [ObservableProperty] private string _clinicId = string.Empty;
    [ObservableProperty] private string _clinicName = string.Empty;
    [ObservableProperty] private string _appointmentType = "FOLLOW-UP";
    [ObservableProperty] private string _priority = "ROUTINE";
    [ObservableProperty] private string? _preferredProvider;
    [ObservableProperty] private DateTime? _desiredStart;
    [ObservableProperty] private DateTime? _desiredEnd;
    [ObservableProperty] private string? _comments;

    public AppointmentWaitListViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null;
        Entries.Clear();

        var siteParams = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await siteParams.IsFeatureEnabledAsync("APPOINTMENT_WAITLIST");

        if (!IsFeatureEnabled) return;

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var entries = await workflow.GetWaitListEntriesAsync();
        foreach (var e in entries) Entries.Add(e);
    }

    [RelayCommand]
    public async Task AddToWaitListAsync()
    {
        if (!HasPatient) return;
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AddToWaitListAsync(
                ClinicId, ClinicName,
                AppointmentType,
                null, PreferredProvider,
                Priority,
                DesiredStart, DesiredEnd,
                Comments,
                "PROVIDER-CURRENT", "Provider, Test");

            SuccessMessage = "Added to wait list successfully.";
            ShowNewForm = false;

            // Reset form fields
            ClinicId = string.Empty;
            ClinicName = string.Empty;
            AppointmentType = "FOLLOW-UP";
            Priority = "ROUTINE";
            PreferredProvider = null;
            DesiredStart = null;
            DesiredEnd = null;
            Comments = null;

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ToggleNewForm()
    {
        ShowNewForm = !ShowNewForm;
        SuccessMessage = null;
    }
}
