// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all ERAs (Electronic Remittance Advices).
/// Grain key: "ERA-IDX"
/// </summary>
public interface IEraIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new entry or updates an existing one (matched by EraId).</summary>
    Task AddOrUpdateAsync(EraIndexEntry entry);

    /// <summary>Returns all ERA entries.</summary>
    Task<List<EraIndexEntry>> GetAllAsync();
}
