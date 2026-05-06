// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-wide index grain for all outbreaks.
/// Key: "HAI-OUTBREAK-IDX"
/// </summary>
public interface IOutbreakIndexGrain : IGrainWithStringKey
{
    Task<List<OutbreakSummary>> GetAllOutbreaksAsync();

    Task<List<OutbreakSummary>> GetActiveAsync();

    Task UpsertOutbreakAsync(OutbreakSummary summary);

    Task RemoveOutbreakAsync(string outbreakId);
}
