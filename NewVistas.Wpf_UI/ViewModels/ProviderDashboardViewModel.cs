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

public partial class ProviderDashboardViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ProviderScheduleEntry> _scheduleEntries = new();
    [ObservableProperty] private ObservableCollection<ProviderPatientEntry> _myPatients = new();
    [ObservableProperty] private string _providerId = string.Empty;
    [ObservableProperty] private string _patientSearch = string.Empty;

    public ProviderDashboardViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) return;
        var schedGrain = Grains.GetGrain<IProviderScheduleIndexGrain>($"PROV-SCHED:{ProviderId.Trim()}");
        List<ProviderScheduleEntry> entries = await schedGrain.GetAllAsync(500);
        ScheduleEntries.Clear();
        foreach (var e in entries) ScheduleEntries.Add(e);
    }

    [RelayCommand]
    private async Task LoadMyPatients()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) return;
        IsLoading = true; Error = null;
        try
        {
            var patIdx = Grains.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{ProviderId.Trim()}");
            List<ProviderPatientEntry> patients = string.IsNullOrWhiteSpace(PatientSearch)
                ? await patIdx.GetActivePatientsAsync()
                : await patIdx.SearchPatientsAsync(PatientSearch.Trim());
            MyPatients.Clear();
            foreach (var p in patients) MyPatients.Add(p);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
