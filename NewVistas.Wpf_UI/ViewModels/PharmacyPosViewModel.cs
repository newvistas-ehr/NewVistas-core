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

public partial class PharmacyPosViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<PosClaimIndexEntry> _claims = new();
    [ObservableProperty] private bool _showPaidOnly;

    public PharmacyPosViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<PosClaimIndexEntry> list = ShowPaidOnly
            ? await workflow.GetPosClaimsByStatusAsync(PosClaimStatus.Paid)
            : await workflow.GetPosClaimsAsync();
        Claims.Clear();
        foreach (PosClaimIndexEntry c in list) Claims.Add(c);
    }
}
