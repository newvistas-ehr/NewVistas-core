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

public partial class AutoRefillViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<AutoRefillIndexEntry> _enrollments = new();
    [ObservableProperty] private bool _showEnrollForm;

    // Form fields
    [ObservableProperty] private string _prescriptionId = string.Empty;
    [ObservableProperty] private string _drugName = string.Empty;
    [ObservableProperty] private string _drugClass = string.Empty;
    [ObservableProperty] private int _daysSupply = 30;
    [ObservableProperty] private int _refillsRemaining = 3;
    [ObservableProperty] private DateTime _lastFillDate = DateTime.Today;
    [ObservableProperty] private string _pharmacyId = string.Empty;
    [ObservableProperty] private string _pharmacyName = string.Empty;

    public AutoRefillViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null; Enrollments.Clear();
        var siteParams = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await siteParams.IsFeatureEnabledAsync("AUTO_REFILL");
        if (!IsFeatureEnabled) return;

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        foreach (var e in await workflow.GetAutoRefillEnrollmentsAsync()) Enrollments.Add(e);
    }

    [RelayCommand]
    public async Task EnrollAsync()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.EnrollAutoRefillAsync(
                PrescriptionId, DrugName, DrugClass,
                DaysSupply, RefillsRemaining, LastFillDate,
                PharmacyId, PharmacyName,
                "PROVIDER-CURRENT", "Provider, Test");
            SuccessMessage = "Enrolled in auto-refill.";
            ShowEnrollForm = false;
            PrescriptionId = string.Empty; DrugName = string.Empty; DrugClass = string.Empty;
            DaysSupply = 30; RefillsRemaining = 3; LastFillDate = DateTime.Today;
            PharmacyId = string.Empty; PharmacyName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void ToggleEnrollForm() { ShowEnrollForm = !ShowEnrollForm; SuccessMessage = null; }
}
