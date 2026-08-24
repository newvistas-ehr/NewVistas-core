// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class FeeBasisViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private FeePatientState? _feePatient;
    [ObservableProperty] private ObservableCollection<FeeAuthorizationIndexEntry> _authorizations = new();
    [ObservableProperty] private ObservableCollection<FeeInvoiceIndexEntry> _invoices = new();
    [ObservableProperty] private ObservableCollection<FeeVendorIndexEntry> _vendors = new();
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _loaded;

    public FeeBasisViewModel(OrleansGrainService grains)
    {
        _grains = grains;
        _ = LoadVendorsAsync();
    }

    private async Task LoadVendorsAsync()
    {
        try
        {
            var vendorIndex = _grains.GetGrain<IFeeVendorIndexGrain>("FEE-VENDOR-IDX");
            List<FeeVendorIndexEntry> list = await vendorIndex.GetAllAsync();
            Vendors.Clear();
            foreach (FeeVendorIndexEntry v in list) Vendors.Add(v);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task Load()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null; Loaded = false;
        try
        {
            string pid = PatientId.Trim();
            var feePatientGrain = _grains.GetGrain<IFeePatientGrain>($"FEE-PATIENT:{pid}");
            FeePatient = await feePatientGrain.GetAsync();

            var authIndex = _grains.GetGrain<IFeeAuthorizationIndexGrain>($"FEE-AUTH-IDX:{pid}");
            List<FeeAuthorizationIndexEntry> auths = await authIndex.GetAllAsync();
            Authorizations.Clear();
            foreach (FeeAuthorizationIndexEntry a in auths) Authorizations.Add(a);

            var invIndex = _grains.GetGrain<IFeeInvoiceIndexGrain>($"FEE-INVOICE-IDX:{pid}");
            List<FeeInvoiceIndexEntry> invs = await invIndex.GetAllAsync();
            Invoices.Clear();
            foreach (FeeInvoiceIndexEntry i in invs) Invoices.Add(i);

            Loaded = true;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
