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

public partial class PrenatalViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<PregnancyIndexEntry> _pregnancies = new();
    [ObservableProperty] private ObservableCollection<PrenatalVisitIndexEntry> _visits = new();
    [ObservableProperty] private string _selectedPregnancyId = string.Empty;
    [ObservableProperty] private PregnancyState? _selectedPregnancyDetail;

    public PrenatalViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<PregnancyIndexEntry> list = await workflow.GetPregnanciesAsync();
        Pregnancies.Clear();
        foreach (PregnancyIndexEntry p in list) Pregnancies.Add(p);

        // Auto-select first active pregnancy
        PregnancyIndexEntry? active = list.FirstOrDefault(p => p.Status == PregnancyStatus.Active);
        if (active != null)
            await LoadVisitsAsync(active.PregnancyId);
    }

    public async Task LoadVisitsAsync(string pregnancyId)
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        SelectedPregnancyId = pregnancyId;
        SelectedPregnancyDetail = await workflow.GetPregnancyAsync(pregnancyId);

        List<PrenatalVisitIndexEntry> visitList = await workflow.GetPrenatalVisitsAsync(pregnancyId);
        Visits.Clear();
        foreach (PrenatalVisitIndexEntry v in visitList) Visits.Add(v);
    }
}
