// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of prior-authorization requests for fast status queries.
/// Grain key: "PA-INDEX:{patientId}"
/// </summary>
public interface IPriorAuthIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(PriorAuthIndexEntry entry);

    Task RemoveAsync(string paId);

    Task<List<PriorAuthIndexEntry>> GetAllAsync();

    Task<List<PriorAuthIndexEntry>> GetPendingAsync();

    Task<List<PriorAuthIndexEntry>> GetApprovedAsync();

    Task ClearAsync();
}
