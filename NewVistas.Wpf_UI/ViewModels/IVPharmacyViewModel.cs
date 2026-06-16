// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class IVPharmacyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<IVAdmixOrderIndexEntry> _orders = new();
    [ObservableProperty] private IVAdmixOrderIndexEntry? _selectedOrder;
    [ObservableProperty] private bool _showActiveOnly = true;

    public IVPharmacyViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = ShowActiveOnly
            ? await workflow.GetActiveIVAdmixOrdersAsync()
            : await workflow.GetIVAdmixOrdersAsync();
        Orders.Clear();
        foreach (var o in list) Orders.Add(o);
    }
}
