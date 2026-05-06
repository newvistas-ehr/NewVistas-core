// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    Task<List<ChartRequestIndexEntry>> GetRequestsByPatientAsync(string patientId);
}
