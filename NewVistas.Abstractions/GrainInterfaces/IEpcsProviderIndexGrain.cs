// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton EPCS provider credential index grain.
/// Key: "EPCS-PROVIDER-IDX"
/// </summary>
public interface IEpcsProviderIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.EpcsProviderIndexEntry>> GetAllAsync();
    Task<List<GrainStates.EpcsProviderIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.EpcsProviderIndexEntry entry);
}
