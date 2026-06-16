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

public partial class ConsultsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ConsultSummary> _consults = new();
    [ObservableProperty] private ConsultState? _selectedConsult;

    // Request form
    [ObservableProperty] private bool _showRequestForm;
    [ObservableProperty] private string _toService = string.Empty;
    [ObservableProperty] private string _urgency = "ROUTINE";
    [ObservableProperty] private string _reasonForRequest = string.Empty;
    [ObservableProperty] private string _requestingProviderName = "Provider, Test";

    public string[] UrgencyOptions { get; } = ["ROUTINE", "URGENT", "STAT"];

    public ConsultsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetConsultsAsync(null, 50);
        Consults.Clear();
        foreach (var c in list) Consults.Add(c);
    }

    [RelayCommand]
    private async Task SelectConsult(ConsultSummary consult)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedConsult = await workflow.GetConsultAsync(consult.ConsultId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleRequestForm() => ShowRequestForm = !ShowRequestForm;

    [RelayCommand]
    private async Task RequestConsult()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ToService)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RequestConsultAsync(
                ToService, null, // toService
                null, null, // fromService
                Urgency,
                null, RequestingProviderName, // requestingProvider
                null, null, // attentionProvider
                ReasonForRequest,
                null, // provisionalDiagnosis
                null, null, null); // orderId, location
            ShowRequestForm = false;
            ToService = string.Empty;
            ReasonForRequest = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AcceptConsult()
    {
        if (SelectedConsult is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AcceptConsultAsync(SelectedConsult.ConsultId);
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CompleteConsult()
    {
        if (SelectedConsult is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CompleteConsultAsync(
                SelectedConsult.ConsultId,
                "Consult completed.",
                null, "Provider, Test");
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
