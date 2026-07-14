// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide home-care census / caseload roster + workload roll-up — the analog of VistA's
/// HBPC <c>HBH</c> workload package. Singleton key: "HHC-CENSUS:DEFAULT".
/// </summary>
public interface IHomeCareCensusGrain : IGrainWithStringKey
{
    Task UpsertEntryAsync(HomeCareCensusEntry entry);
    Task RemoveEntryAsync(string episodeId);
    Task<List<HomeCareCensusEntry>> GetAllAsync();
    Task<List<HomeCareCensusEntry>> GetActiveAsync();
    Task<List<HomeCareCensusEntry>> GetByLevelOfCareAsync(HomeCareLevelOfCare levelOfCare);
    Task<List<HomeCareCensusEntry>> GetByDeliveryModelAsync(HomeCareDeliveryModel deliveryModel);
    Task<List<HomeCareCensusEntry>> GetByProviderAsync(string providerId);
    Task<List<HomeCareCensusEntry>> GetWithUpcomingVisitsAsync(int withinDays);
    Task<List<HomeCareCensusEntry>> GetWithNoRecentVisitAsync(int daysSinceLastVisit);
    Task<HomeCareWorkloadStats> GetWorkloadStatsAsync();
}
