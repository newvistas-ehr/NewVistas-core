// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton GPRA report index grain.
/// Key: "GPRA-REPORT-IDX"
/// </summary>
public interface IGpraReportIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.GpraReportIndexEntry>> GetAllAsync();
    Task<List<GrainStates.GpraReportIndexEntry>> GetByFiscalYearAsync(int fiscalYear);
    Task AddEntryAsync(GrainStates.GpraReportIndexEntry entry);
    Task UpdateStatusAsync(string reportId, GrainStates.GpraReportStatus status);
}
