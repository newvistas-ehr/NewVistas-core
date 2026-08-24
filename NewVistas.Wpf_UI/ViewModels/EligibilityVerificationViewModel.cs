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

public partial class EligibilityVerificationViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<EligibilityVerificationIndexEntry> _history = new();
    [ObservableProperty] private EligibilityInquiryState? _selectedInquiry;
    [ObservableProperty] private string? _actionMessage;

    // New inquiry fields
    [ObservableProperty] private string _newPayerId = string.Empty;
    [ObservableProperty] private string _newPayerName = string.Empty;
    [ObservableProperty] private string _newSubscriberId = string.Empty;

    public EligibilityVerificationViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<EligibilityVerificationIndexEntry> list = await workflow.GetEligibilityVerificationHistoryAsync();
        History.Clear();
        foreach (var h in list) History.Add(h);
        SelectedInquiry = null;
    }

    [RelayCommand]
    private async Task ViewDetail(EligibilityVerificationIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedInquiry = await workflow.GetEligibilityInquiryAsync(entry.InquiryId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task SubmitInquiry()
    {
        if (string.IsNullOrWhiteSpace(NewPayerId) || string.IsNullOrWhiteSpace(NewSubscriberId)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.SubmitEligibilityInquiryAsync(
                null, null, NewPayerId.Trim(), NewPayerName.Trim(), NewSubscriberId.Trim(), null, "SELF", null,
                new List<string>(), DateTime.Today, null, null, null);
            ActionMessage = $"Inquiry submitted: {id}";
            NewPayerId = string.Empty; NewPayerName = string.Empty; NewSubscriberId = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
