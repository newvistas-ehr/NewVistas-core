// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ClaimStatusInquiryViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ClaimStatusInquiryIndexEntry> _inquiries = new();
    [ObservableProperty] private ClaimStatusInquiryState? _selectedInquiry;
    [ObservableProperty] private string? _actionMessage;

    // Submit fields
    [ObservableProperty] private string _newClaimId = string.Empty;
    [ObservableProperty] private string _newPayerId = string.Empty;
    [ObservableProperty] private string _newPayerName = string.Empty;

    public ClaimStatusInquiryViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<ClaimStatusInquiryIndexEntry> list = await workflow.GetClaimStatusInquiriesAsync();
        Inquiries.Clear();
        foreach (var i in list) Inquiries.Add(i);
        SelectedInquiry = null;
    }

    [RelayCommand]
    private async Task ViewDetail(ClaimStatusInquiryIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedInquiry = await workflow.GetClaimStatusInquiryAsync(entry.InquiryId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task SubmitInquiry()
    {
        if (string.IsNullOrWhiteSpace(NewClaimId)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.SubmitClaimStatusInquiryAsync(NewClaimId.Trim(), NewPayerId.Trim(), NewPayerName.Trim(), null, null, null);
            ActionMessage = $"Inquiry submitted: {id}";
            NewClaimId = string.Empty; NewPayerId = string.Empty; NewPayerName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
