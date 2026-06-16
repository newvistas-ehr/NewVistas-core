// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class iCareDashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private string _providerId = string.Empty;
    [ObservableProperty] private ObservableCollection<PanelPatient> _panel = new();
    [ObservableProperty] private ObservableCollection<iCarePatientSummary> _patientSummaries = new();
    [ObservableProperty] private int _totalPatients;
    [ObservableProperty] private int _patientsWithGaps;
    [ObservableProperty] private int _totalDueReminders;
    [ObservableProperty] private int _totalQualityGaps;
    [ObservableProperty] private string _newPatientId = string.Empty;
    [ObservableProperty] private string _newPatientName = string.Empty;

    public iCareDashboardViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
        CheckFeatureCommand = new AsyncRelayCommand(CheckFeatureAsync);
    }

    public IAsyncRelayCommand CheckFeatureCommand { get; }

    private async Task CheckFeatureAsync()
    {
        try
        {
            var siteGrain = _grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            IsFeatureEnabled = await siteGrain.IsFeatureEnabledAsync("ICARE_DASHBOARD");
        }
        catch { IsFeatureEnabled = false; }
    }

    [RelayCommand]
    private async Task LoadPanelAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) { Error = "Provider ID is required."; return; }
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = _grains.GetGrain<IiCareDashboardGrain>($"ICARE:{ProviderId.Trim()}");
            List<PanelPatient> list = await grain.GetPanelAsync();
            Panel.Clear();
            foreach (PanelPatient p in list) Panel.Add(p);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AddToPanelAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) { Error = "Provider ID is required."; return; }
        if (string.IsNullOrWhiteSpace(NewPatientId)) { Error = "Patient ID is required."; return; }
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = _grains.GetGrain<IiCareDashboardGrain>($"ICARE:{ProviderId.Trim()}");
            await grain.AddPatientToPanelAsync(NewPatientId.Trim(), NewPatientName.Trim());
            SuccessMessage = "Patient added to panel.";
            NewPatientId = string.Empty;
            NewPatientName = string.Empty;
            await LoadPanelAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RemoveFromPanelAsync(string patientId)
    {
        if (string.IsNullOrWhiteSpace(ProviderId) || string.IsNullOrWhiteSpace(patientId)) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = _grains.GetGrain<IiCareDashboardGrain>($"ICARE:{ProviderId.Trim()}");
            await grain.RemovePatientFromPanelAsync(patientId.Trim());
            SuccessMessage = "Patient removed from panel.";
            await LoadPanelAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task GenerateDashboardAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) { Error = "Provider ID is required."; return; }
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = _grains.GetGrain<IiCareDashboardGrain>($"ICARE:{ProviderId.Trim()}");
            iCareDashboardResult result = await grain.GenerateDashboardAsync();
            TotalPatients = result.TotalPatients;
            PatientsWithGaps = result.PatientsWithGaps;
            TotalDueReminders = result.TotalDueReminders;
            TotalQualityGaps = result.TotalQualityGaps;
            PatientSummaries.Clear();
            foreach (iCarePatientSummary s in result.PatientSummaries) PatientSummaries.Add(s);
            SuccessMessage = "Dashboard generated successfully.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
