// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// ED Visit Grain — manages a single Emergency Department visit/encounter.
/// Key: "ED-VISIT:{visitId}"
/// Maps to VistA EDIS (Emergency Department Integration Software).
/// </summary>
public interface IEdVisitGrain : IGrainWithStringKey
{
    Task<GrainStates.EdVisitState> GetVisitAsync();

    Task RegisterArrivalAsync(
        string patientId, string patientName, string chiefComplaint,
        string arrivalMode, DateTime arrivalDateTime);

    Task TriageAsync(int acuityLevel, string nurseId, string nurseName);

    Task AssignBedAsync(string bedName);

    Task AssignPhysicianAsync(string physicianId, string physicianName);

    Task AssignNurseAsync(string nurseId, string nurseName);

    Task RecordPhysicianSeenAsync();

    Task UpdateStatusAsync(string status);

    Task UpdateBoardCommentAsync(string comment);

    Task UpdatePendingResultsAsync(string pendingResults);

    Task DischargeAsync(string diagnosis);

    Task AdmitAsync(string destinationWard);

    Task TransferAsync(string destinationFacility);

    Task RecordLwbsAsync();

    Task RecordAmaAsync();
}

/// <summary>
/// ED Tracking Board Grain — singleton per facility, manages the live ED board.
/// Key: "ED-BOARD:{facilityId}" (use "ED-BOARD:MAIN" for single-facility).
/// Provides real-time view of all active ED patients.
/// </summary>
public interface IEdBoardGrain : IGrainWithStringKey
{
    Task<GrainStates.EdBoardState> GetBoardAsync();

    Task<List<GrainStates.EdBoardEntry>> GetActiveVisitsAsync();

    Task<List<GrainStates.EdBoardEntry>> GetWaitingPatientsAsync();

    Task AddOrUpdateVisitAsync(GrainStates.EdBoardEntry entry);

    Task RemoveVisitAsync(string visitId);

    Task<int> GetWaitingCountAsync();

    Task<int> GetOccupiedBedCountAsync();
}
