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

public partial class DrugUtilizationReviewViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<DurAssessmentIndexEntry> _assessments = new();
    [ObservableProperty] private ObservableCollection<DurAssessmentIndexEntry> _pendingReviews = new();
    [ObservableProperty] private DurAssessmentState? _selectedAssessment;
    [ObservableProperty] private string? _actionMessage;

    // Perform DUR form fields
    [ObservableProperty] private string _prescriptionId = string.Empty;
    [ObservableProperty] private string _drugName = string.Empty;
    [ObservableProperty] private string _drugId = string.Empty;
    [ObservableProperty] private string _performedBy = string.Empty;

    public DrugUtilizationReviewViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<DurAssessmentIndexEntry> all = await workflow.GetDurAssessmentsAsync();
        Assessments.Clear();
        PendingReviews.Clear();
        foreach (var a in all)
        {
            Assessments.Add(a);
            if (a.Status == DurAssessmentStatus.Pending || a.Status == DurAssessmentStatus.Failed)
                PendingReviews.Add(a);
        }
        SelectedAssessment = null;
    }

    [RelayCommand]
    private async Task ViewDetail(DurAssessmentIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedAssessment = await workflow.GetDurAssessmentAsync(entry.AssessmentId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task PerformDur()
    {
        if (string.IsNullOrWhiteSpace(PrescriptionId) || string.IsNullOrWhiteSpace(DrugName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.PerformDurAsync(
                PrescriptionId.Trim(), DrugName.Trim(),
                string.IsNullOrWhiteSpace(DrugId) ? null : DrugId.Trim(),
                null, null, null, null, null, null, null, null, false, null,
                string.IsNullOrWhiteSpace(PerformedBy) ? null : PerformedBy.Trim(),
                null, null);
            ActionMessage = $"DUR completed: {id}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AcknowledgeAssessment()
    {
        if (SelectedAssessment is null) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AcknowledgeDurAsync(SelectedAssessment.AssessmentId, "RPH-CURRENT", null);
            ActionMessage = "Assessment acknowledged.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
