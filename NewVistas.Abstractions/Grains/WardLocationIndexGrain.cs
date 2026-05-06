// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index grain for ward locations (VistA File #42).
/// Grain key: "WARD-LOCATION-INDEX"
/// </summary>
public class WardLocationIndexGrain : Grain, IWardLocationIndexGrain
{
    private readonly IPersistentState<WardLocationIndexState> _state;

    public WardLocationIndexGrain(
        [PersistentState("wardLocationState", "wardLocationStore")] IPersistentState<WardLocationIndexState> state)
    {
        _state = state;
    }

    public async Task EnsureSeedDataAsync()
    {
        if (_state.State.IsSeeded) return;

        _state.State.Wards.AddRange(new[]
        {
            new WardLocationEntry { WardId = "WARD-MED-3A",  Name = "Medical Ward 3A",  WardType = "MEDICINE", Division = "Main Campus", BedCapacity = 28, IsActive = true },
            new WardLocationEntry { WardId = "WARD-MED-4B",  Name = "Medical Ward 4B",  WardType = "MEDICINE", Division = "Main Campus", BedCapacity = 30, IsActive = true },
            new WardLocationEntry { WardId = "WARD-SURG-2C", Name = "Surgery Ward 2C",  WardType = "SURGERY",  Division = "Main Campus", BedCapacity = 24, IsActive = true },
            new WardLocationEntry { WardId = "WARD-ICU-1",   Name = "Intensive Care Unit", WardType = "ICU",   Division = "Main Campus", BedCapacity = 12, IsActive = true },
            new WardLocationEntry { WardId = "WARD-PSYCH-5A",Name = "Psychiatry 5A",    WardType = "PSYCH",    Division = "Main Campus", BedCapacity = 20, IsActive = true },
            new WardLocationEntry { WardId = "WARD-OBS-1",   Name = "Observation Unit", WardType = "OBS",      Division = "Main Campus", BedCapacity = 15, IsActive = true },
        });

        _state.State.IsSeeded = true;
        await _state.WriteStateAsync();
    }

    public async Task<List<WardLocationEntry>> GetAllWardsAsync()
    {
        await EnsureSeedDataAsync();
        return _state.State.Wards.Where(w => w.IsActive).ToList();
    }

    public async Task<List<WardLocationEntry>> SearchWardsAsync(string query)
    {
        await EnsureSeedDataAsync();
        string q = query.ToUpperInvariant();
        return _state.State.Wards
            .Where(w => w.IsActive &&
                   (w.Name.ToUpperInvariant().Contains(q) || w.WardType.ToUpperInvariant().Contains(q)))
            .ToList();
    }

    public async Task AddWardAsync(WardLocationEntry entry)
    {
        if (!_state.State.Wards.Any(w => w.WardId == entry.WardId))
            _state.State.Wards.Add(entry);
        await _state.WriteStateAsync();
    }
}
