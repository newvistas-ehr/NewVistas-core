// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of PCE encounter visits (File #9000010).
/// Grain key: "PCE-VISITS:{patientId}"
/// </summary>
public interface IPatientVisitIndexGrain : IGrainWithStringKey
{
    Task<List<PceVisitEntry>> GetVisitsAsync(int maxResults);
    Task AddOrUpdateVisitAsync(PceVisitEntry entry);
    Task RemoveVisitAsync(string visitId);
}
