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

public partial class BloodBankViewModel : BasePatientViewModel
{
    [ObservableProperty] private BloodBankPatientState? _patient;
    [ObservableProperty] private ObservableCollection<CrossmatchIndexEntry> _crossmatches = new();
    [ObservableProperty] private ObservableCollection<TransfusionIndexEntry> _transfusions = new();

    public BloodBankViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Patient = await workflow.GetBloodBankPatientAsync();

        var crossmatches = await workflow.GetCrossmatchesAsync();
        Crossmatches.Clear();
        foreach (var c in crossmatches) Crossmatches.Add(c);

        var transfusions = await workflow.GetTransfusionHistoryAsync();
        Transfusions.Clear();
        foreach (var t in transfusions) Transfusions.Add(t);
    }
}
