// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide chart request index grain.
/// Singleton key: "RT-REQUEST-IDX".
/// </summary>
public interface IChartRequestIndexGrain : IGrainWithStringKey
{
    Task UpsertRequestAsync(ChartRequestIndexEntry entry);
    Task<List<ChartRequestIndexEntry>> GetAllRequestsAsync();
    Task<List<ChartRequestIndexEntry>> GetPendingRequestsAsync();
    Task<List<ChartRequestIndexEntry>> GetUrgentRequestsAsync();
    Task<List<ChartRequestIndexEntry>> GetRequestsByPatientAsync(string patientId, int maxResults = 50);
}
