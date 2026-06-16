// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient Audit Index Grain — per-patient ordered list of audit event summaries.
///
/// Mirrors VistA AUDIT file (#1.1) patient-centric query pattern from XUSEC QUERY.
/// Stores lightweight summaries so audit trail listing requires only this single grain,
/// not a full activation of each individual AuditEventGrain.
///
/// Grain Key: patientId (same key as IPatientWorkflowGrain)
/// </summary>
public interface IPatientAuditIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add a new audit event summary to this patient's index.
    /// Called immediately after the AuditEventGrain is written.
    /// </summary>
    Task AddEventAsync(GrainStates.AuditEventSummary summary);

    /// <summary>
    /// Get the most recent N audit events, newest first.
    /// </summary>
    Task<List<GrainStates.AuditEventSummary>> GetRecentEventsAsync(int maxResults = 100);

    /// <summary>
    /// Get audit events filtered by domain and/or date range.
    /// Equivalent to VistA XUSEC QUERY with FILE and DATE filters.
    /// </summary>
    Task<List<GrainStates.AuditEventSummary>> GetEventsAsync(
        string? domain,
        DateTime? from,
        DateTime? to,
        int maxResults = 200);

    /// <summary>
    /// Get audit events for a specific entity (e.g., all events for a given order).
    /// </summary>
    Task<List<GrainStates.AuditEventSummary>> GetEventsByEntityAsync(
        string entityType,
        string entityId);
}
