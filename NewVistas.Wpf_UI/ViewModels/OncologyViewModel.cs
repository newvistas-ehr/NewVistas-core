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

public partial class OncologyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<OncologyTumorIndexEntry> _tumors = new();
    [ObservableProperty] private ObservableCollection<OncologyTreatmentIndexEntry> _treatments = new();
    [ObservableProperty] private OncologyTumorIndexEntry? _selectedTumor;

    public OncologyViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);

        var tumors = await workflow.GetOncologyTumorsAsync();
        Tumors.Clear();
        foreach (var t in tumors) Tumors.Add(t);

        var treatments = await workflow.GetOncologyTreatmentsAsync();
        Treatments.Clear();
        foreach (var t in treatments) Treatments.Add(t);
    }
}
