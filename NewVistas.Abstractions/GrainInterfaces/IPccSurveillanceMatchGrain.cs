// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// PCC Surveillance Match Grain — RPMS APCSSIL2.m encounter record.
/// Key: "PCC-SURV-MATCH:{matchId}"
///
/// Records a single encounter that matched surveillance criteria.
/// </summary>
public interface IPccSurveillanceMatchGrain : IGrainWithStringKey
{
    Task<GrainStates.PccSurveillanceMatchState> GetAsync();

    Task CreateAsync(
        string patientId, string? patientName,
        string configId, string conditionName,
        GrainStates.PccEncounterClassification classification,
        DateTime encounterDate, GrainStates.PccVisitType visitType,
        string? chiefComplaint, string? facilityName,
        DateTime? dischargeDate, string? providerName,
        List<string>? matchingDiagnoses,
        List<string>? matchingProcedures,
        List<string>? matchingLabResults,
        List<string>? matchingMedications,
        GrainStates.PccComorbidityFlags? comorbidities,
        GrainStates.PccEncounterVitals? vitals);

    Task UpdateStatusAsync(GrainStates.PccSurveillanceMatchStatus status);
    Task MarkExportedAsync(string exportReference);
}
