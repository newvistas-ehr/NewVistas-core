// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a patient's Home-Based Primary Care (HBPC) program record.
/// Key pattern: "HBPC-PATIENT:{patientId}".
/// VistA File #750 (HOME BASED PRIMARY CARE). HBPC.m
/// </summary>
public interface IHBPCPatientGrain : IGrainWithStringKey
{
    Task EnrollPatientAsync(
        string patientId,
        string patientName,
        DateTime enrollmentDate,
        HBPCLevelOfCare levelOfCare,
        string primaryDiagnosis,
        string primaryCaregiver,
        string homeAddress);

    Task UpdateLevelOfCareAsync(HBPCLevelOfCare levelOfCare);
    Task AddGoalAsync(string goal);
    Task AddCareTeamMemberAsync(string memberNameAndRole);
    Task AddSecondaryDiagnosisAsync(string diagnosis);
    Task SuspendEnrollmentAsync();
    Task ReactivateEnrollmentAsync();
    Task RecordVisitAsync(DateTime visitDate, DateTime? nextScheduledVisit);
    Task DischargePatientAsync(HBPCDischargeReason reason, string dischargeNotes);
    Task MarkDeceasedAsync(string notes);
    Task<HBPCPatientState> GetPatientAsync();
}
