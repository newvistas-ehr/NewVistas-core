// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Audit Report Grain — stores a generated formal audit report.
/// §170.315(d)(3) — Audit Report(s).
/// </summary>
public class AuditReportGrain : Grain, IAuditReportGrain
{
    private readonly IPersistentState<AuditReportState> _state;

    public AuditReportGrain(
        [PersistentState("auditReportState", "auditReportStore")] IPersistentState<AuditReportState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReportId))
            _state.State.ReportId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SaveReportAsync(AuditReportState report)
    {
        _state.State.ReportId = report.ReportId;
        _state.State.Title = report.Title;
        _state.State.ReportType = report.ReportType;
        _state.State.PatientId = report.PatientId;
        _state.State.UserId = report.UserId;
        _state.State.DomainFilter = report.DomainFilter;
        _state.State.ActionFilter = report.ActionFilter;
        _state.State.PeriodStart = report.PeriodStart;
        _state.State.PeriodEnd = report.PeriodEnd;
        _state.State.GeneratedDate = report.GeneratedDate;
        _state.State.GeneratedBy = report.GeneratedBy;
        _state.State.TotalEvents = report.TotalEvents;
        _state.State.EventsByDomain = report.EventsByDomain;
        _state.State.EventsByAction = report.EventsByAction;
        _state.State.EventsByUser = report.EventsByUser;
        _state.State.Events = report.Events;
        _state.State.IntegrityPassCount = report.IntegrityPassCount;
        _state.State.IntegrityFailCount = report.IntegrityFailCount;
        _state.State.IntegrityFailures = report.IntegrityFailures;
        _state.State.IntegrityStatus = report.IntegrityStatus;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<AuditReportState> GetReportAsync() => Task.FromResult(_state.State);
}

/// <summary>
/// Audit Report Index Grain — listing of all generated reports.
/// </summary>
public class AuditReportIndexGrain : Grain, IAuditReportIndexGrain
{
    private readonly IPersistentState<AuditReportIndexState> _state;

    public AuditReportIndexGrain(
        [PersistentState("auditReportIndexState", "auditReportIndexStore")] IPersistentState<AuditReportIndexState> state)
    {
        _state = state;
    }

    public async Task AddReportAsync(AuditReportSummary summary)
    {
        _state.State.Reports.RemoveAll(r => r.ReportId == summary.ReportId);
        _state.State.Reports.Add(summary);
        await _state.WriteStateAsync();
    }

    public Task<List<AuditReportSummary>> GetAllReportsAsync()
        => Task.FromResult(_state.State.Reports.OrderByDescending(r => r.GeneratedDate).ToList());

    public Task<List<AuditReportSummary>> GetReportsByPatientAsync(string patientId, int maxResults = 50)
        => Task.FromResult(_state.State.Reports
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.GeneratedDate).Take(maxResults).ToList());

    public Task<List<AuditReportSummary>> GetReportsByTypeAsync(string reportType)
        => Task.FromResult(_state.State.Reports
            .Where(r => r.ReportType.Equals(reportType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.GeneratedDate).ToList());
}

/// <summary>
/// Audit Report Generator Grain — generates a formal audit report for a patient.
/// Reads the patient's audit index, applies filters, computes aggregation statistics,
/// and optionally verifies hash-chain integrity on each event.
///
/// Grain Key: "AUDIT-REPORT-GEN:{patientId}"
/// </summary>
public class AuditReportGeneratorGrain : Grain, IAuditReportGeneratorGrain
{
    private readonly IGrainFactory _grainFactory;

    public AuditReportGeneratorGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<AuditReportState> GenerateReportAsync(
        DateTime periodStart,
        DateTime periodEnd,
        string? domainFilter,
        string? actionFilter,
        string? userIdFilter,
        bool verifyIntegrity,
        string? generatedBy)
    {
        string key = this.GetPrimaryKeyString();
        int colonIdx = key.IndexOf(':');
        string patientId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;

        // Get all events in the date range from the patient's audit index
        IPatientAuditIndexGrain auditIndex = _grainFactory.GetGrain<IPatientAuditIndexGrain>(patientId);
        List<AuditEventSummary> allEvents = await auditIndex.GetEventsAsync(null, periodStart, periodEnd, 10000);

        // Apply additional filters
        IEnumerable<AuditEventSummary> filtered = allEvents;

        if (!string.IsNullOrEmpty(domainFilter))
            filtered = filtered.Where(e => e.Domain.Equals(domainFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(actionFilter))
            filtered = filtered.Where(e => e.Action.Equals(actionFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(userIdFilter))
        {
            // Need to load full events to filter by userId (summary only has UserName)
            var filteredByUser = new List<AuditEventSummary>();
            foreach (AuditEventSummary evt in filtered)
            {
                IAuditEventGrain eventGrain = _grainFactory.GetGrain<IAuditEventGrain>(evt.EventId);
                AuditEventState fullEvent = await eventGrain.GetEventAsync();
                if (fullEvent.UserId == userIdFilter)
                    filteredByUser.Add(evt);
            }
            filtered = filteredByUser;
        }

        List<AuditEventSummary> reportEvents = filtered.ToList();

        // Compute aggregation statistics
        Dictionary<string, int> byDomain = reportEvents
            .GroupBy(e => e.Domain)
            .ToDictionary(g => g.Key, g => g.Count());

        Dictionary<string, int> byAction = reportEvents
            .GroupBy(e => e.Action)
            .ToDictionary(g => g.Key, g => g.Count());

        Dictionary<string, int> byUser = reportEvents
            .Where(e => !string.IsNullOrEmpty(e.UserName))
            .GroupBy(e => e.UserName!)
            .ToDictionary(g => g.Key, g => g.Count());

        // Integrity verification
        int passCount = 0;
        int failCount = 0;
        List<string> failures = new();
        string integrityStatus = "not-checked";

        if (verifyIntegrity)
        {
            foreach (AuditEventSummary evt in reportEvents)
            {
                IAuditEventGrain eventGrain = _grainFactory.GetGrain<IAuditEventGrain>(evt.EventId);
                bool valid = await eventGrain.VerifyIntegrityAsync();
                if (valid)
                    passCount++;
                else
                {
                    failCount++;
                    failures.Add(evt.EventId);
                }
            }
            integrityStatus = failCount > 0 ? "tamper-detected" : "verified";
        }

        // Build title
        string titleParts = $"{periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}";
        if (!string.IsNullOrEmpty(domainFilter)) titleParts += $", Domain: {domainFilter}";
        string title = $"Patient Audit Report — {titleParts}";

        string reportId = Guid.NewGuid().ToString("N");

        return new AuditReportState
        {
            ReportId = reportId,
            Title = title,
            ReportType = "patient",
            PatientId = patientId,
            UserId = userIdFilter,
            DomainFilter = domainFilter,
            ActionFilter = actionFilter,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GeneratedDate = DateTime.UtcNow,
            GeneratedBy = generatedBy,
            TotalEvents = reportEvents.Count,
            EventsByDomain = byDomain,
            EventsByAction = byAction,
            EventsByUser = byUser,
            Events = reportEvents,
            IntegrityPassCount = passCount,
            IntegrityFailCount = failCount,
            IntegrityFailures = failures,
            IntegrityStatus = integrityStatus,
            LastModifiedDate = DateTime.UtcNow
        };
    }
}
