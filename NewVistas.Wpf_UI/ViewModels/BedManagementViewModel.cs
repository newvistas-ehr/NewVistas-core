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

/// <summary>
/// Bed management board — reads the institution's IBedCapacityGrain for the
/// unit directory/counts and drills into each IInpatientUnitGrain for bed rows.
/// Status actions are the unit grain's EVS turnover operations.
/// </summary>
public partial class BedManagementViewModel : ObservableObject
{
    private const string InstitutionId = "500";

    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<BedBoardEntry> _beds = new();
    [ObservableProperty] private BedBoardEntry? _selectedBed;
    [ObservableProperty] private string _wardFilter = string.Empty;
    [ObservableProperty] private BedBoardStats? _stats;

    public BedManagementViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var capacityGrain = _grains.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{InstitutionId}");
            List<UnitCapacitySummary> units = await capacityGrain.GetUnitsAsync(true);

            Beds.Clear();
            int total = 0, occupied = 0, available = 0, cleaning = 0;
            foreach (UnitCapacitySummary u in units)
            {
                total += u.TotalBeds;
                occupied += u.Occupied;
                available += u.Available;
                cleaning += u.Dirty + u.Cleaning;

                if (!string.IsNullOrEmpty(WardFilter) &&
                    !u.UnitId.Equals(WardFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                var unitGrain = _grains.GetGrain<IInpatientUnitGrain>($"UNIT:{InstitutionId}:{u.UnitId}");
                InpatientUnitState state = await unitGrain.GetAsync();
                foreach (InpatientBed b in state.Beds)
                {
                    Beds.Add(new BedBoardEntry(
                        b.BedId, u.UnitId, b.State.ToString(), b.PatientName,
                        b.Isolation == BedIsolationType.None ? null : b.Isolation.ToString()));
                }
            }

            Stats = new BedBoardStats(total, occupied, available, cleaning);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    /// <summary>Dirty/Cleaning → Available (EVS turnover complete).</summary>
    [RelayCommand]
    private Task MarkAvailableAsync()
        => RunBedOperationAsync(g => g.MarkBedCleanAsync(SelectedBed!.BedId, null));

    /// <summary>Dirty → Cleaning (EVS turnover started).</summary>
    [RelayCommand]
    private Task StartCleaningAsync()
        => RunBedOperationAsync(g => g.StartCleaningAsync(SelectedBed!.BedId, null));

    /// <summary>Available → Dirty (spill / contamination).</summary>
    [RelayCommand]
    private Task MarkDirtyAsync()
        => RunBedOperationAsync(g => g.MarkBedDirtyAsync(SelectedBed!.BedId));

    private async Task RunBedOperationAsync(Func<IInpatientUnitGrain, Task> operation)
    {
        if (SelectedBed == null) return;
        try
        {
            var unitGrain = _grains.GetGrain<IInpatientUnitGrain>($"UNIT:{InstitutionId}:{SelectedBed.WardId}");
            await operation(unitGrain);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public record BedBoardEntry(string BedId, string WardId, string Status, string? PatientName, string? IsolationType);
public record BedBoardStats(int TotalBeds, int OccupiedBeds, int AvailableBeds, int CleaningBeds);
