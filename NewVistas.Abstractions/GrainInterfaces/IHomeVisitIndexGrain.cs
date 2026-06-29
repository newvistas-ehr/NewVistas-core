// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide home-visit index — supports the daily visit schedule and per-clinician
/// caseloads (the in-home mobile clients' primary query). Singleton key: "HHC-VISIT-INDEX".
/// </summary>
public interface IHomeVisitIndexGrain : IGrainWithStringKey
{
    Task UpsertVisitAsync(HomeVisitIndexEntry entry);
    Task RemoveVisitAsync(string visitId);
    Task<List<HomeVisitIndexEntry>> GetVisitsByEpisodeAsync(string episodeId);
    Task<List<HomeVisitIndexEntry>> GetVisitsByClinicianAsync(string clinicianId);
    Task<List<HomeVisitIndexEntry>> GetVisitsInRangeAsync(DateTime start, DateTime end);
    Task<List<HomeVisitIndexEntry>> GetUpcomingVisitsAsync(int withinDays);
}
