// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Pregnancy Grain — IHS Prenatal Care Module (File #90680.01) and
/// RPMS Women's Health pregnancy tracking (BJPNAPI.m, BWGRVL.m).
/// Key: "OB-PREG:{guid}"
///
/// Models a single pregnancy record with obstetric history (GPAL),
/// EDD calculation, risk factors, prenatal problems, delivery info,
/// and postpartum follow-up.
/// </summary>
public interface IPregnancyGrain : IGrainWithStringKey
{
    Task<GrainStates.PregnancyState> GetAsync();

    Task CreateAsync(
        string patientId,
        DateTime? lastMenstrualPeriod,
        DateTime? eddByLmp,
        DateTime? eddByUltrasound,
        DateTime definitiveEdd,
        int gravida,
        int para,
        int abortions,
        int living,
        GrainStates.PregnancyRiskLevel riskLevel,
        List<string>? riskFactors,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? notes);

    /// <summary>Updates risk assessment for the pregnancy.</summary>
    Task UpdateRiskAsync(GrainStates.PregnancyRiskLevel riskLevel, List<string> riskFactors);

    /// <summary>Adds a prenatal problem to the pregnancy.</summary>
    Task AddProblemAsync(GrainStates.PrenatalProblemEntry problem);

    /// <summary>Resolves (deactivates) a prenatal problem by ID.</summary>
    Task ResolveProblemAsync(string problemId);

    /// <summary>Records delivery information and transitions status.</summary>
    Task RecordDeliveryAsync(GrainStates.DeliveryInfo delivery, GrainStates.PregnancyOutcome outcome);

    /// <summary>Links a newborn record (NEONATE:{guid}) delivered from this pregnancy (supports multiples).</summary>
    Task AddNewbornIdAsync(string newbornId);

    /// <summary>Records postpartum follow-up information.</summary>
    Task RecordPostpartumAsync(GrainStates.PostpartumInfo postpartum);

    /// <summary>Updates the pregnancy status (e.g., to Cancelled, Ectopic).</summary>
    Task UpdateStatusAsync(GrainStates.PregnancyStatus status);

    /// <summary>Updates the definitive EDD.</summary>
    Task UpdateEddAsync(DateTime? eddByUltrasound, DateTime definitiveEdd);
}
