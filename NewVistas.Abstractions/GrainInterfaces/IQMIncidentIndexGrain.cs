// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// System-wide index of all occurrence screen / patient safety incident reports.
/// Grain key: "QM-INCIDENT-IDX" (singleton)
/// </summary>
public interface IQMIncidentIndexGrain : IGrainWithStringKey
{
    /// <summary>Inserts or updates an incident summary entry in the index.</summary>
    Task UpsertIncidentAsync(GrainStates.QMIncidentIndexEntry entry);

    /// <summary>Returns all incident summaries, newest occurrence date first.</summary>
    Task<List<GrainStates.QMIncidentIndexEntry>> GetAllIncidentsAsync();

    /// <summary>Returns incidents matching the given severity level.</summary>
    Task<List<GrainStates.QMIncidentIndexEntry>> GetIncidentsBySeverityAsync(GrainStates.OccurrenceSeverity severity);

    /// <summary>Returns incidents matching the given workflow status.</summary>
    Task<List<GrainStates.QMIncidentIndexEntry>> GetIncidentsByStatusAsync(GrainStates.IncidentStatus status);

    /// <summary>Returns all incidents involving a specific patient.</summary>
    /// <param name="maxResults">Maximum entries returned, newest first. Bounds the payload; full history is available by passing a larger value.</param>
    Task<List<GrainStates.QMIncidentIndexEntry>> GetIncidentsByPatientAsync(string patientId, int maxResults = 50);

    /// <summary>Returns incidents matching the given occurrence category.</summary>
    Task<List<GrainStates.QMIncidentIndexEntry>> GetIncidentsByCategoryAsync(GrainStates.OccurrenceCategory category);
}
