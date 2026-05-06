// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-wide index grain for all HAI cases.
/// Key: "HAI-CASE-IDX"
/// </summary>
public interface IHAICaseIndexGrain : IGrainWithStringKey
{
    Task<List<HAICaseSummary>> GetAllCasesAsync();

    Task<List<HAICaseSummary>> GetActiveAsync();

    Task<List<HAICaseSummary>> GetByTypeAsync(HAIType haiType);

    Task<List<HAICaseSummary>> GetByLocationAsync(string locationId);

    Task<List<HAICaseSummary>> GetByOutbreakAsync(string outbreakId);

    Task UpsertCaseAsync(HAICaseSummary summary);

    Task RemoveCaseAsync(string caseId);
}
