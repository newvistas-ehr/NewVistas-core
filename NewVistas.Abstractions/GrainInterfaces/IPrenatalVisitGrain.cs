// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Prenatal Visit Grain — IHS Prenatal Care Module V OB file (9000010.43).
/// Key: "OB-VISIT:{guid}"
///
/// Models a single prenatal encounter with maternal vitals, obstetric exam
/// findings, dipstick results, and cervical exam data.
/// </summary>
public interface IPrenatalVisitGrain : IGrainWithStringKey
{
    Task<GrainStates.PrenatalVisitState> GetAsync();

    Task CreateAsync(
        string pregnancyId,
        string patientId,
        DateTime visitDate,
        int gestationalAgeWeeks,
        int gestationalAgeDays,
        decimal? weight,
        int? bloodPressureSystolic,
        int? bloodPressureDiastolic,
        decimal? fundalHeightCm,
        int? fetalHeartRate,
        GrainStates.FetalPresentation fetalPresentation,
        bool? fetalMovement,
        string? urineProtein,
        string? urineGlucose,
        string? edema,
        decimal? cervicalDilationCm,
        int? cervicalEffacementPercent,
        int? fetalStation,
        string? providerId,
        string? providerName,
        string? notes,
        DateTime? nextVisitDate);

    /// <summary>Updates notes on an existing prenatal visit.</summary>
    Task UpdateNotesAsync(string? notes);
}
