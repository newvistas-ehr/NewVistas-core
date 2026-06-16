// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Crossmatch Grain — represents a single crossmatch/compatibility test request.
///
/// Derived from VistA Blood Bank module (BBCM.m, BBTM.m):
///   File #65.03  — CROSSMATCH (patient, unit, compatibility result)
///
/// Grain key: "BB-XM:{crossmatchId}"
/// </summary>
public interface ICrossmatchGrain : IGrainWithStringKey
{
    Task<CrossmatchState> GetCrossmatchAsync();

    /// <summary>Creates a new crossmatch request for a patient and blood unit.</summary>
    Task CreateAsync(
        string patientId,
        string unitId,
        CrossmatchUrgency urgency,
        string requestedByUserId,
        string requestedByUserName,
        string? patientAboType,
        string? patientRhType,
        string? unitAboType,
        string? unitRhType,
        string? notes);

    /// <summary>Records the compatibility test result.</summary>
    Task RecordResultAsync(
        CrossmatchResult result,
        CrossmatchMethod method,
        string technicianId,
        string technicianName,
        string? antibodyIdentification);

    /// <summary>Marks the unit as issued for transfusion.</summary>
    Task IssueUnitAsync(string issuedByUserId, string issuedByUserName, string transfusionId);

    /// <summary>Cancels the crossmatch (e.g., patient discharged, clinician cancelled).</summary>
    Task CancelAsync(string cancelledByUserId, string? reason);
}
