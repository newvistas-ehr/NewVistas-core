// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Crossmatch Index Grain — per-patient index of all crossmatch requests.
///
/// Grain key: "BB-XM-IDX:{patientId}"
/// </summary>
public interface ICrossmatchIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(CrossmatchIndexEntry entry);
    Task<List<CrossmatchIndexEntry>> GetAllAsync();
}
