// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
