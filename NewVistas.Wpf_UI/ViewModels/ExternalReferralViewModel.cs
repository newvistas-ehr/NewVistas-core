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

public partial class ExternalReferralViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<ExternalReferralIndexEntry> _referrals = new();
    [ObservableProperty] private bool _showNewForm;

    // Form fields
    [ObservableProperty] private string _referralType = "SPECIALTY";
    [ObservableProperty] private string _facilityName = string.Empty;
    [ObservableProperty] private string? _providerName;
    [ObservableProperty] private string _purpose = string.Empty;
    [ObservableProperty] private string? _diagnosis;
    [ObservableProperty] private string _urgency = "ROUTINE";
    [ObservableProperty] private string? _authNumber;
    [ObservableProperty] private string? _specialInstructions;

    public ExternalReferralViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null;
        Referrals.Clear();

        var siteParams = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await siteParams.IsFeatureEnabledAsync("EXTERNAL_REFERRAL_TRACKING");

        if (!IsFeatureEnabled) return;

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var referrals = await workflow.GetExternalReferralsAsync();
        foreach (var r in referrals) Referrals.Add(r);
    }

    [RelayCommand]
    public async Task CreateReferralAsync()
    {
        if (!HasPatient) return;
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateExternalReferralAsync(
                ReferralType, FacilityName, null,
                ProviderName, null, Purpose, Diagnosis,
                Urgency, "PROVIDER-CURRENT", "Provider, Test",
                null, AuthNumber, null, SpecialInstructions);

            SuccessMessage = "Referral created successfully.";
            ShowNewForm = false;

            // Reset form fields
            ReferralType = "SPECIALTY";
            FacilityName = string.Empty;
            ProviderName = null;
            Purpose = string.Empty;
            Diagnosis = null;
            Urgency = "ROUTINE";
            AuthNumber = null;
            SpecialInstructions = null;

            // Reload data
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
