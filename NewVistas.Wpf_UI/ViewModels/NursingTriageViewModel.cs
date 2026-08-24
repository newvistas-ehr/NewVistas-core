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

public partial class NursingTriageViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<NursingTriageIndexEntry> _triages = new();
    [ObservableProperty] private NursingTriageState? _selectedTriage;
    [ObservableProperty] private string? _actionMessage;

    // New triage fields
    [ObservableProperty] private string _chiefComplaint = string.Empty;
    [ObservableProperty] private string _nurseId = string.Empty;
    [ObservableProperty] private string _nurseName = string.Empty;

    public NursingTriageViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<NursingTriageIndexEntry> list = await workflow.GetTriageAssessmentsAsync();
        Triages.Clear();
        foreach (var t in list) Triages.Add(t);
        SelectedTriage = null;
    }

    [RelayCommand]
    private async Task ViewDetail(NursingTriageIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedTriage = await workflow.GetTriageAssessmentAsync(entry.TriageId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateTriage()
    {
        if (string.IsNullOrWhiteSpace(ChiefComplaint)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.CreateTriageAssessmentAsync(
                DateTime.UtcNow, NurseId, NurseName, null, null, ChiefComplaint, null,
                null, null, null, null, null, null, null, TriageLevel.Esi3_Urgent,
                null, null, "AMBULATORY", false, false, null);
            ActionMessage = $"Triage created: {id}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SignTriage()
    {
        if (SelectedTriage is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.SignTriageAssessmentAsync(SelectedTriage.TriageId, NurseId, NurseName);
            ActionMessage = "Triage signed.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
