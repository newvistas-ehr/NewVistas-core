// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Center Index Grain — singleton index of all BR training centers.
///
/// Grain key: "BR-CENTER-IDX"
/// </summary>
public interface IBRCenterIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all registered BR training centers.</summary>
    Task<List<BRCenterIndexEntry>> GetAllAsync();

    /// <summary>Returns only centers currently accepting new patients.</summary>
    Task<List<BRCenterIndexEntry>> GetAcceptingAsync();

    /// <summary>Adds or updates a center entry in the index.</summary>
    Task UpsertAsync(BRCenterIndexEntry entry);

    /// <summary>Seeds the index with known VA blind rehabilitation centers.</summary>
    Task SeedDefaultsAsync();
}
