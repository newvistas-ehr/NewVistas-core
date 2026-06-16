// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
