// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Rad tech worklist — per-location pending exams queue.
/// Grain key: "RAD-WORKLIST:{locationId}"
/// </summary>
public interface IRadWorklistGrain : IGrainWithStringKey
{
    Task<RadWorklistState> GetAsync();
    Task RefreshAsync(List<RadWorklistItem> items);
    Task AddItemAsync(RadWorklistItem item);
    Task RemoveItemAsync(string radiologyId);
    Task<List<RadWorklistItem>> GetByStatusAsync(RadExamStatus status);
    Task<List<RadWorklistItem>> GetUrgentAsync();
}
