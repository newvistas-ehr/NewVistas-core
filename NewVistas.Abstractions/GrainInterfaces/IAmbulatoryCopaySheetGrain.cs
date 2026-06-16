// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Ambulatory Copay Check-Off Sheet grain (VistA File #350.7 AMBULATORY CHECK-OFF SHEET).
/// Documents billable items delivered during an outpatient clinic visit,
/// ensuring all copay-generating services are captured before the encounter closes.
///
/// MUMPS routines: IBCOA*.m (check-off sheet actions).
/// Grain key: "IB-SHEET:{guid}"
/// </summary>
public interface IAmbulatoryCopaySheetGrain : IGrainWithStringKey
{
    /// <summary>Returns the full check-off sheet record.</summary>
    Task<GrainStates.AmbulatoryCopaySheetState> GetAsync();

    /// <summary>
    /// Initializes a new check-off sheet for a patient visit.
    /// Returns the sheet ID (same as the grain key suffix).
    /// </summary>
    Task<string> CreateAsync(
        string patientId,
        string? encounterId,
        DateTime visitDate,
        string? clinicId,
        string? clinicName);

    /// <summary>
    /// Marks a specific item as checked (billable) on the sheet.
    /// If the item code doesn't exist in the checklist, adds it.
    /// </summary>
    Task CheckItemAsync(
        string itemCode,
        string itemDescription,
        string checkedByUserId,
        string checkedByUserName);

    /// <summary>
    /// Marks the sheet as complete — all applicable items have been reviewed.
    /// Sets IsComplete = true and records who completed it.
    /// </summary>
    Task CompleteAsync(string completedByUserId, string completedByUserName);

    /// <summary>Links a generated billing action ID to this sheet for traceability.</summary>
    Task LinkBillingActionAsync(string billingActionId);
}
