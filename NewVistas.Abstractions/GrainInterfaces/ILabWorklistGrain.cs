// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab tech worklist grain — aggregated view of pending specimens, results, and verifications.
/// Grain key: "LAB-WORKLIST:{locationId}"
/// </summary>
public interface ILabWorklistGrain : IGrainWithStringKey
{
    Task<LabWorklistState> GetAsync();
    Task RefreshAsync(List<LabWorklistItem> items);
    Task AddItemAsync(LabWorklistItem item);
    Task RemoveItemAsync(string labTestId);
    Task<List<LabWorklistItem>> GetByCategoryAsync(LabWorklistCategory category);
    Task<List<LabWorklistItem>> GetCriticalAsync();
}
