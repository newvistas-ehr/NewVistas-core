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

public partial class AnatomicPathologyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<APCaseIndexEntry> _cases = new();
    [ObservableProperty] private APCaseIndexEntry? _selectedCase;
    [ObservableProperty] private AnatomicPathologyState? _caseDetail;

    public AnatomicPathologyViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetAPCasesAsync();
        Cases.Clear();
        foreach (var c in list) Cases.Add(c);
    }

    [RelayCommand]
    private async Task SelectCase(APCaseIndexEntry entry)
    {
        SelectedCase = entry;
        CaseDetail = null;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            CaseDetail = await workflow.GetAPCaseAsync(entry.CaseId);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
