// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-episode SA visit index grain.
/// Key: "SA-VISIT-IDX:{episodeId}"
/// </summary>
public interface ISAVisitIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.SAVisitIndexEntry>> GetAllAsync();
    Task AddEntryAsync(GrainStates.SAVisitIndexEntry entry);
    Task<int> GetVisitCountAsync();
}
