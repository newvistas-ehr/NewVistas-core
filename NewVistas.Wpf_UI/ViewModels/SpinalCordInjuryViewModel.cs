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

public partial class SpinalCordInjuryViewModel : BasePatientViewModel
{
    [ObservableProperty] private SCIPatientState? _patient;
    [ObservableProperty] private ObservableCollection<SCIAnnualEncounterRecord> _encounters = new();

    public SpinalCordInjuryViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Patient = await workflow.GetSCIPatientAsync();
        var list = await workflow.GetSCIAnnualEncountersAsync();
        Encounters.Clear();
        foreach (var e in list) Encounters.Add(e);
    }
}
