// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class BedManagementViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<BedBoardEntry> _beds = new();
    [ObservableProperty] private BedBoardEntry? _selectedBed;
    [ObservableProperty] private string _wardFilter = string.Empty;
    [ObservableProperty] private BedBoardStats? _stats;

    public BedManagementViewModel(ApiClient api, OrleansGrainService grains)
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
            var boardGrain = _grains.GetGrain<IBedBoardGrain>("BED-BOARD:DEFAULT");
            List<BedSummaryEntry> allBeds = await boardGrain.GetAllBedsAsync();
            Beds.Clear();
            foreach (BedSummaryEntry b in allBeds)
            {
                if (string.IsNullOrEmpty(WardFilter) || b.WardId == WardFilter)
                    Beds.Add(new BedBoardEntry(b.BedId, b.WardId, b.Status, b.PatientName, b.IsolationType));
            }

            int total = await boardGrain.GetTotalBedCountAsync();
            int available = await boardGrain.GetAvailableBedCountAsync();
            int occupied = await boardGrain.GetOccupiedBedCountAsync();
            Stats = new BedBoardStats(total, occupied, available, total - occupied - available);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task MarkAvailableAsync()
    {
        if (SelectedBed == null) return;
        try
        {
            var bedGrain = _grains.GetGrain<IBedGrain>($"BED:DEFAULT:{SelectedBed.BedId}");
            await bedGrain.SetAvailableAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task DischargeAsync()
    {
        if (SelectedBed == null) return;
        try
        {
            var bedGrain = _grains.GetGrain<IBedGrain>($"BED:DEFAULT:{SelectedBed.BedId}");
            await bedGrain.DischargePatientAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public record BedBoardEntry(string BedId, string WardId, string Status, string? PatientName, string? IsolationType);
public record BedBoardStats(int TotalBeds, int OccupiedBeds, int AvailableBeds, int CleaningBeds);
