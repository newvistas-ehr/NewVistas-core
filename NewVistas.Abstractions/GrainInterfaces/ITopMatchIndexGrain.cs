// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of TOP offset matching records.
/// Grain key: "TOP-MATCH-IDX"
/// </summary>
public interface ITopMatchIndexGrain : IGrainWithStringKey
{
    Task<List<TopMatchIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(TopMatchIndexEntry entry);
    Task<List<TopMatchIndexEntry>> GetPendingAsync();
    Task<List<TopMatchIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
}
