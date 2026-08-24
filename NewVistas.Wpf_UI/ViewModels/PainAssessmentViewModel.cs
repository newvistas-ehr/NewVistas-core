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

public partial class PainAssessmentViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<PainAssessmentIndexEntry> _assessments = new();
    [ObservableProperty] private PainAssessmentState? _selectedAssessment;
    [ObservableProperty] private string? _actionMessage;

    // New assessment fields
    [ObservableProperty] private int _painScore;
    [ObservableProperty] private string _painLocation = string.Empty;
    [ObservableProperty] private string _nurseId = string.Empty;

    public PainAssessmentViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<PainAssessmentIndexEntry> list = await workflow.GetPainAssessmentsAsync();
        Assessments.Clear();
        foreach (var a in list) Assessments.Add(a);
        SelectedAssessment = null;
    }

    [RelayCommand]
    private async Task ViewDetail(PainAssessmentIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedAssessment = await workflow.GetPainAssessmentAsync(entry.AssessmentId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task RecordPain()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.RecordPainAssessmentAsync(
                PainAssessmentTool.NumericRatingScale, PainScore, PainLocation,
                "Sharp", "Acute", null, null, null, null, null, null, null,
                NurseId, "Nurse", null);
            ActionMessage = $"Assessment recorded: {id}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
