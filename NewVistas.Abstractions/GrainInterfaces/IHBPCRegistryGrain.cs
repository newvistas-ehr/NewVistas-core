// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide HBPC patient registry.
/// Singleton key: "HBPC-REGISTRY".
/// </summary>
public interface IHBPCRegistryGrain : IGrainWithStringKey
{
    Task UpsertPatientAsync(HBPCRegistryEntry entry);
    Task<List<HBPCRegistryEntry>> GetAllPatientsAsync();
    Task<List<HBPCRegistryEntry>> GetActivePatientsAsync();
    Task<List<HBPCRegistryEntry>> GetPatientsByLevelOfCareAsync(HBPCLevelOfCare levelOfCare);
    Task<List<HBPCRegistryEntry>> GetPatientsWithUpcomingVisitsAsync(int withinDays);
    Task<List<HBPCRegistryEntry>> GetPatientsWithNoRecentVisitAsync(int daysSinceLastVisit);
}
