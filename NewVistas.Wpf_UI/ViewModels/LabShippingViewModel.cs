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

public partial class LabShippingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ShippingManifestState? _manifest;
    [ObservableProperty] private ShippingConfigState? _config;
    [ObservableProperty] private string _manifestIdInput = string.Empty;
    [ObservableProperty] private string _configIdInput = string.Empty;
    [ObservableProperty] private string? _actionMessage;

    // Ship fields
    [ObservableProperty] private string _shipTrackingNumber = string.Empty;

    public LabShippingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        if (string.IsNullOrWhiteSpace(ManifestIdInput)) return;
        var grain = Grains.GetGrain<IShippingManifestGrain>($"LA-SHIP:{ManifestIdInput.Trim()}");
        Manifest = await grain.GetManifestAsync();
    }

    [RelayCommand]
    private async Task LoadConfig()
    {
        if (string.IsNullOrWhiteSpace(ConfigIdInput)) return;
        Error = null;
        try
        {
            var grain = Grains.GetGrain<IShippingConfigGrain>($"LA-SHIPCFG:{ConfigIdInput.Trim()}");
            Config = await grain.GetConfigAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ShipManifest()
    {
        if (Manifest is null) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IShippingManifestGrain>($"LA-SHIP:{Manifest.ManifestId}");
            await grain.ShipManifestAsync(string.IsNullOrWhiteSpace(ShipTrackingNumber) ? null : ShipTrackingNumber.Trim());
            ActionMessage = "Manifest shipped.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task MarkReceived()
    {
        if (Manifest is null) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IShippingManifestGrain>($"LA-SHIP:{Manifest.ManifestId}");
            await grain.MarkReceivedAsync();
            ActionMessage = "Manifest marked as received.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
