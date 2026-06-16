// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class EmergencyDepartmentViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<EdBoardEntry> _board = new();
    [ObservableProperty] private EdBoardEntry? _selectedVisit;
    [ObservableProperty] private EdStatsVm? _stats;
    [ObservableProperty] private bool _showRegisterForm;

    // Register form
    [ObservableProperty] private string _regPatientId = string.Empty;
    [ObservableProperty] private string _regPatientName = string.Empty;
    [ObservableProperty] private string _chiefComplaint = string.Empty;
    [ObservableProperty] private string _arrivalMode = "WALK_IN";
    [ObservableProperty] private string _assignBedId = string.Empty;

    public string[] ArrivalModes { get; } = ["WALK_IN", "AMBULANCE", "HELICOPTER", "TRANSFER", "POLICE"];

    public EmergencyDepartmentViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var boardGrain = _grains.GetGrain<IEdBoardGrain>("ED-BOARD:MAIN");
            List<EdBoardEntry> active = await boardGrain.GetActiveVisitsAsync();
            Board.Clear();
            foreach (EdBoardEntry e in active) Board.Add(e);

            int waiting = await boardGrain.GetWaitingCountAsync();
            int inTreatment = await boardGrain.GetOccupiedBedCountAsync();
            Stats = new EdStatsVm(active.Count, waiting, inTreatment, 0);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleRegisterForm() => ShowRegisterForm = !ShowRegisterForm;

    [RelayCommand]
    private async Task RegisterPatientAsync()
    {
        try
        {
            string visitId = $"ED-VISIT:{Guid.NewGuid()}";
            var visitGrain = _grains.GetGrain<IEdVisitGrain>(visitId);
            await visitGrain.RegisterArrivalAsync(
                RegPatientId, RegPatientName, ChiefComplaint, ArrivalMode, DateTime.UtcNow);
            ShowRegisterForm = false;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task AssignBedAsync()
    {
        if (SelectedVisit == null || string.IsNullOrWhiteSpace(AssignBedId)) return;
        try
        {
            var visitGrain = _grains.GetGrain<IEdVisitGrain>($"ED-VISIT:{SelectedVisit.VisitId}");
            await visitGrain.AssignBedAsync(AssignBedId);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task DischargeAsync()
    {
        if (SelectedVisit == null) return;
        try
        {
            var visitGrain = _grains.GetGrain<IEdVisitGrain>($"ED-VISIT:{SelectedVisit.VisitId}");
            await visitGrain.DischargeAsync("Discharged");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            await _api.Http.PostAsJsonAsync("api/edis/demo/load", new { });
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public record EdStatsVm(int TotalPatients, int Waiting, int InTreatment, double AvgWaitMinutes);
