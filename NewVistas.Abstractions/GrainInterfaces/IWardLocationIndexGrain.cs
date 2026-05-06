// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of all ward locations.
/// VistA WARD LOCATION file (#42) — MUMPS reference: DGPMWL.m
/// Grain key: "WARD-LOCATION-INDEX"
/// </summary>
public interface IWardLocationIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all active ward locations, seeding demo data on first call.</summary>
    Task<List<WardLocationEntry>> GetAllWardsAsync();

    /// <summary>Case-insensitive search on ward name or type.</summary>
    Task<List<WardLocationEntry>> SearchWardsAsync(string query);

    /// <summary>Adds a new ward location entry.</summary>
    Task AddWardAsync(WardLocationEntry entry);

    /// <summary>Seeds demo ward data if not already seeded.</summary>
    Task EnsureSeedDataAsync();
}
