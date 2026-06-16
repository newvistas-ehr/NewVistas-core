// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class DataSegmentationViewModel : BasePatientViewModel
{
    [ObservableProperty] private Ds4pAnalysisResult? _analysisResult;
    [ObservableProperty] private string? _actionMessage;

    // Analyze fields
    [ObservableProperty] private string _messageId = string.Empty;
    [ObservableProperty] private string _ccdaContent = string.Empty;

    public DataSegmentationViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        // Load existing analysis for the patient if available
        if (!string.IsNullOrWhiteSpace(MessageId))
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            AnalysisResult = await workflow.GetDs4pAnalysisAsync(MessageId.Trim());
        }
    }

    [RelayCommand]
    private async Task AnalyzeCcda()
    {
        if (string.IsNullOrWhiteSpace(CcdaContent)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            AnalysisResult = await workflow.AnalyzeDs4pCcdaAsync(
                string.IsNullOrWhiteSpace(MessageId) ? Guid.NewGuid().ToString() : MessageId.Trim(),
                CcdaContent);
            ActionMessage = "Analysis complete.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
