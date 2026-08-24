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

public partial class BlindRehabilitationViewModel : BasePatientViewModel
{
    [ObservableProperty] private BRPatientState? _patient;
    [ObservableProperty] private ObservableCollection<BRAdmissionIndexEntry> _admissions = new();
    [ObservableProperty] private ObservableCollection<BROutpatientVisitIndexEntry> _outpatientVisits = new();

    public BlindRehabilitationViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Patient = await workflow.GetBRPatientAsync();

        var admissions = await workflow.GetBRAdmissionsAsync();
        Admissions.Clear();
        foreach (var a in admissions) Admissions.Add(a);

        var visits = await workflow.GetBROutpatientVisitsAsync();
        OutpatientVisits.Clear();
        foreach (var v in visits) OutpatientVisits.Add(v);
    }
}
