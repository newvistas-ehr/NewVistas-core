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

public partial class AnesthesiaTrackingViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<AnesthesiaRecordIndexEntry> _records = new();

    public AnesthesiaTrackingViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null; Records.Clear();
        var sp = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await sp.IsFeatureEnabledAsync("ANESTHESIA_TRACKING");
        if (!IsFeatureEnabled) return;

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        foreach (var r in await workflow.GetAnesthesiaRecordsAsync()) Records.Add(r);
    }
}
