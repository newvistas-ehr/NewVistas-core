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

public partial class BeneficiaryTravelViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<BeneficiaryTravelClaimEntry> _claims = new();
    [ObservableProperty] private BeneficiaryTravelClaimEntry? _selectedClaim;
    [ObservableProperty] private bool _showFileForm;

    // File claim form fields
    [ObservableProperty] private DateTime _travelDate = DateTime.Today;
    [ObservableProperty] private string _mileage = string.Empty;
    [ObservableProperty] private bool _roundTrip = true;
    [ObservableProperty] private string _transportMode = "POV";
    [ObservableProperty] private string _originAddress = string.Empty;
    [ObservableProperty] private string _destinationAddress = string.Empty;
    [ObservableProperty] private string _eligibilityCode = string.Empty;
    [ObservableProperty] private bool _deductibleExempt;

    public string[] TransportModes { get; } = ["POV", "BUS", "AIRLINE", "TAXI", "SPECIAL_MODE", "COMMON_CARRIER"];

    public BeneficiaryTravelViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var index = Grains.GetGrain<IBeneficiaryTravelIndexGrain>($"DGBT-INDEX:{PatientId}");
        var list = await index.GetClaimsAsync();
        Claims.Clear();
        foreach (var c in list) Claims.Add(c);
    }

    [RelayCommand]
    private void ToggleFileForm() => ShowFileForm = !ShowFileForm;

    [RelayCommand]
    private async Task FileClaimAsync()
    {
        try
        {
            string claimId = $"DGBT:{Guid.NewGuid()}";
            var claim = Grains.GetGrain<IBeneficiaryTravelClaimGrain>(claimId);
            decimal miles = decimal.TryParse(Mileage, out var m) ? m : 0;
            await claim.CreateClaimAsync(
                PatientId, string.Empty, TravelDate,
                "MILEAGE", miles, RoundTrip,
                TransportMode, OriginAddress, DestinationAddress,
                null, EligibilityCode, DeductibleExempt);

            // Update index
            var index = Grains.GetGrain<IBeneficiaryTravelIndexGrain>($"DGBT-INDEX:{PatientId}");
            await index.AddOrUpdateAsync(new BeneficiaryTravelClaimEntry
            {
                ClaimId = claimId,
                TravelDate = TravelDate,
                ClaimType = "MILEAGE",
                TotalMileage = RoundTrip ? miles * 2 : miles,
                Status = "PENDING"
            });

            ShowFileForm = false;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ApproveClaimAsync()
    {
        if (SelectedClaim == null) return;
        try
        {
            var claim = Grains.GetGrain<IBeneficiaryTravelClaimGrain>(SelectedClaim.ClaimId);
            await claim.ApproveAsync("Provider, Test");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task DenyClaimAsync()
    {
        if (SelectedClaim == null) return;
        try
        {
            var claim = Grains.GetGrain<IBeneficiaryTravelClaimGrain>(SelectedClaim.ClaimId);
            await claim.DenyAsync("Denied by provider", "Provider, Test");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task PayClaimAsync()
    {
        if (SelectedClaim == null) return;
        try
        {
            var claim = Grains.GetGrain<IBeneficiaryTravelClaimGrain>(SelectedClaim.ClaimId);
            await claim.RecordPaymentAsync("DIRECT_DEPOSIT", DateTime.UtcNow);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
