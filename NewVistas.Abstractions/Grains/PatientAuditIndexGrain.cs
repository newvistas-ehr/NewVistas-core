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
/// Patient Audit Index Grain — per-patient ordered log of audit event summaries.
/// Provides fast listing/filtering without activating individual AuditEventGrains.
/// Mirrors VistA XUSEC QUERY pattern against the AUDIT file (#1.1).
/// </summary>
public class PatientAuditIndexGrain : Grain, IPatientAuditIndexGrain
{
    private readonly IPersistentState<PatientAuditIndexState> _state;

    public PatientAuditIndexGrain(
        [PersistentState("patientAuditIndexState", "patientAuditIndexStore")] IPersistentState<PatientAuditIndexState> state)
    {
        _state = state;
    }

    public async Task AddEventAsync(AuditEventSummary summary)
    {
        _state.State.Events.Add(summary);
        await _state.WriteStateAsync();
    }

    public Task<List<AuditEventSummary>> GetRecentEventsAsync(int maxResults = 100)
    {
        var results = _state.State.Events
            .OrderByDescending(e => e.Timestamp)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<AuditEventSummary>> GetEventsAsync(
        string? domain,
        DateTime? from,
        DateTime? to,
        int maxResults = 200)
    {
        IEnumerable<AuditEventSummary> query = _state.State.Events;

        if (!string.IsNullOrEmpty(domain))
            query = query.Where(e => e.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);

        var results = query
            .OrderByDescending(e => e.Timestamp)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<AuditEventSummary>> GetEventsByEntityAsync(string entityType, string entityId)
    {
        var results = _state.State.Events
            .Where(e => e.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase)
                     && e.EntityId.Equals(entityId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
        return Task.FromResult(results);
    }
}
