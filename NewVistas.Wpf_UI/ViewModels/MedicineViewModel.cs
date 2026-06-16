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

public partial class MedicineViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<MedProcedureIndexEntry> _procedures = new();
    [ObservableProperty] private MedProcedureIndexEntry? _selectedProcedure;
    [ObservableProperty] private bool _showCompletedOnly;

    public MedicineViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = ShowCompletedOnly
            ? await workflow.GetCompletedMedProceduresAsync()
            : await workflow.GetMedProceduresAsync();
        Procedures.Clear();
        foreach (var p in list) Procedures.Add(p);
    }
}
