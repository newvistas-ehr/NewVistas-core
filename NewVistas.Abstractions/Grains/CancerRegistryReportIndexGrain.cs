// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Cancer Registry Report Index Grain — tracks all cancer registry reports.
/// §170.315(f)(4) — Transmission to cancer registries.
///
/// System-level singleton index for listing and filtering reports.
///
/// Grain Key: "CR-REPORT-INDEX"
/// </summary>
public class CancerRegistryReportIndexGrain : Grain, ICancerRegistryReportIndexGrain
{
    private readonly IPersistentState<CancerRegistryReportIndexState> _state;

    public CancerRegistryReportIndexGrain(
        [PersistentState("cancerRegistryReportIndex", "crReportIndexStore")]
        IPersistentState<CancerRegistryReportIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertReportAsync(CancerRegistryReportIndexEntry entry)
    {
        CancerRegistryReportIndexEntry? existing = _state.State.Reports
            .FirstOrDefault(r => r.ReportId == entry.ReportId);

        if (existing != null)
        {
            existing.Status = entry.Status;
            existing.RegistryName = entry.RegistryName;
        }
        else
        {
            _state.State.Reports.Add(entry);
        }

        await _state.WriteStateAsync();
    }

    public Task<List<CancerRegistryReportIndexEntry>> GetAllReportsAsync()
        => Task.FromResult(_state.State.Reports.ToList());

    public Task<List<CancerRegistryReportIndexEntry>> GetReportsByPatientAsync(string patientId)
        => Task.FromResult(_state.State.Reports
            .Where(r => r.PatientId == patientId).ToList());

    public Task<List<CancerRegistryReportIndexEntry>> GetReportsByStatusAsync(string status)
    {
        if (Enum.TryParse<CancerRegistryReportStatus>(status, true, out CancerRegistryReportStatus s))
            return Task.FromResult(_state.State.Reports.Where(r => r.Status == s).ToList());
        return Task.FromResult(new List<CancerRegistryReportIndexEntry>());
    }

    public Task<List<CancerRegistryReportIndexEntry>> GetPendingReportsAsync()
        => Task.FromResult(_state.State.Reports
            .Where(r => r.Status == CancerRegistryReportStatus.Generated).ToList());
}
