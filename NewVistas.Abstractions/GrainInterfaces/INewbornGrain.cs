// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A newborn's neonatal chart — birth through nursery discharge. Registered from the mother's
/// delivery. Key pattern: "NEONATE:{guid}".
/// </summary>
public interface INewbornGrain : IGrainWithStringKey
{
    /// <summary>Registers the newborn at birth; classifies GA / birth-weight / size-for-GA.</summary>
    Task RegisterAsync(
        string motherPatientId,
        string pregnancyId,
        string name,
        NewbornSex sex,
        DateTime birthDateTime,
        int gestationalAgeWeeks,
        int gestationalAgeDays,
        DeliveryMethod deliveryMethod,
        int? birthWeightGrams,
        decimal? lengthCm,
        decimal? headCircumferenceCm,
        int? apgar1Min,
        int? apgar5Min,
        int? apgar10Min,
        int multipleBirthOrder,
        int multipleBirthTotal,
        string attendingProviderId,
        string attendingProviderName,
        string birthLocationName);

    Task RecordExamAsync(NewbornExam exam);

    /// <summary>Records a screen result (upsert by screening type — one current result per screen).</summary>
    Task RecordScreeningAsync(NewbornScreeningEntry screening);

    Task AddMeasurementAsync(NewbornMeasurement measurement);

    Task SetNurseryLevelAsync(NurseryLevelOfCare level, string reason);

    Task TransferAsync(string toLocation, string reason);

    Task DischargeAsync(
        DateTime dischargeDateTime,
        int? dischargeWeightGrams,
        NewbornFeedingType dischargeFeeding,
        string disposition,
        string followUpPlan,
        bool carSeatTestPassed);

    // ── NICU depth (Phase 2) ──────────────────────────────────────────────
    /// <summary>Records a respiratory-support change (closes the previous open episode).</summary>
    Task RecordRespiratorySupportAsync(RespiratorySupportEntry entry);
    Task StartPhototherapyAsync(PhototherapyEntry entry);
    Task EndPhototherapyAsync(DateTime endedAt, string notes);
    Task AddProblemAsync(NeonatalProblemEntry problem);
    Task ResolveProblemAsync(string problemId);
    Task AddNutritionAsync(NeonatalNutritionEntry entry);
    Task AddProcedureAsync(NeonatalProcedureEntry procedure);

    Task<NewbornState> GetAsync();
}
