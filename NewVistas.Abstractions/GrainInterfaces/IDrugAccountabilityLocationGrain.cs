// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-location drug inventory index grain — lightweight balance projection for
/// ward pharmacists and dispensing cabinet screens.
/// Grain key: "DA-LOC:{locationId}"
/// </summary>
public interface IDrugAccountabilityLocationGrain : IGrainWithStringKey
{
    Task<DrugAccountabilityLocationState> GetLocationAsync();

    Task InitialiseLocationAsync(string locationId, string locationName, string locationType);

    Task AddOrUpdateDrugAsync(DrugBalanceSummary summary);

    Task<List<DrugBalanceSummary>> GetAllDrugsAsync();

    /// <summary>Returns drugs where CurrentBalance &lt;= ReorderPoint (when ReorderPoint is set).</summary>
    Task<List<DrugBalanceSummary>> GetLowStockDrugsAsync();

    Task<List<DrugBalanceSummary>> GetControlledSubstancesAsync();

    Task ClearAsync();
}
