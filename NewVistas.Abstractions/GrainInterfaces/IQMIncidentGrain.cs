// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single occurrence screen / patient safety incident report.
/// VistA File #680 (OCCURRENCE SCREEN). PXRM.m, QMEVNT.m.
/// Grain key: "QM-INCIDENT:{guid}"
/// </summary>
public interface IQMIncidentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Creates the occurrence screen report with initial identification data.
    /// Status → Reported.
    /// </summary>
    Task ReportIncidentAsync(
        string patientId,
        string patientName,
        DateTime occurrenceDate,
        GrainStates.OccurrenceCategory category,
        string description,
        string location,
        string wardUnit,
        GrainStates.OccurrenceSeverity severity,
        string reportedBy,
        string reportedByTitle,
        string immediateAction,
        string diagnosisAtTime,
        string procedureAtTime,
        string medicationInvolved,
        string equipmentInvolved);

    /// <summary>Updates the clinical outcome and notification status.</summary>
    Task UpdateOutcomeAsync(
        string outcomeDescription,
        bool patientNotified,
        bool familyNotified);

    /// <summary>Adds a staff member's name to the incident record.</summary>
    Task AddStaffInvolvedAsync(string staffName);

    /// <summary>Associates a quality review with this incident. Status → PeerReviewAssigned or RCAInProgress.</summary>
    Task AddReviewToIncidentAsync(string reviewId, GrainStates.QMReviewType reviewType);

    /// <summary>Records the identified root cause and corrective actions summary.</summary>
    Task SetRootCauseIdentifiedAsync(bool identified, string correctiveActionsSummary);

    /// <summary>Closes the incident after all reviews are complete. Status → Closed.</summary>
    Task CloseIncidentAsync();

    /// <summary>Voids the incident report with a reason (duplicate or entered in error). Status → Voided.</summary>
    Task VoidIncidentAsync(string reason);

    /// <summary>Returns the full state of this incident record.</summary>
    Task<GrainStates.QMIncidentState> GetIncidentAsync();
}
