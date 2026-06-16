// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide chart index grain.
/// Singleton key: "RT-CHART-IDX".
/// </summary>
public interface IChartIndexGrain : IGrainWithStringKey
{
    Task UpsertChartAsync(ChartIndexEntry entry);
    Task<List<ChartIndexEntry>> GetAllChartsAsync();
    Task<List<ChartIndexEntry>> GetCheckedOutChartsAsync();
    Task<List<ChartIndexEntry>> GetChartsOnRequestAsync();
    Task<List<ChartIndexEntry>> GetLostChartsAsync();
    Task<List<ChartIndexEntry>> GetOverdueChartsAsync();
    Task<ChartIndexEntry?> GetChartByPatientAsync(string patientId);
}
