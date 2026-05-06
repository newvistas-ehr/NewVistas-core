// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient home health visit history index.
/// Key pattern: "HHC-VISIT-IDX:{patientId}".
/// </summary>
public interface IHHCVisitIndexGrain : IGrainWithStringKey
{
    Task UpsertVisitAsync(HHCVisitIndexEntry entry);
    Task<List<HHCVisitIndexEntry>> GetAllVisitsAsync();
    Task<List<HHCVisitIndexEntry>> GetVisitsByDisciplineAsync(HHCVisitDiscipline discipline);
    Task<List<HHCVisitIndexEntry>> GetUpcomingVisitsAsync();
    Task<List<HHCVisitIndexEntry>> GetCompletedVisitsAsync();
}
