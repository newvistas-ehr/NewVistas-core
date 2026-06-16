// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient-per-registry clinical case registry entry — key: "CCR:{RegistryType}:{patientId}"
/// Holds all condition-specific data for one patient's enrollment in one registry type.
/// </summary>
public interface IClinicalRegistryEntryGrain : IGrainWithStringKey
{
    Task<ClinicalRegistryEntryState> GetEntryAsync();

    Task EnrollPatientAsync(
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        RegistryType registryType,
        string enrolledById,
        string enrolledByName,
        string siteId,
        string siteName,
        string primaryProviderId,
        string primaryProviderName,
        string? notes);

    Task UpdateEnrollmentStatusAsync(CCREnrollmentStatus status, DateTime? deactivationDate, string? reason);

    Task UpdateHIVDataAsync(
        HIVStage stage,
        decimal? cd4Count,
        DateTime? cd4Date,
        decimal? viralLoadCopies,
        DateTime? viralLoadDate,
        bool isVirallySuppressed,
        DateTime? artStartDate,
        string? artRegimen);

    Task UpdateHepCDataAsync(
        HepCGenotype genotype,
        decimal? fibrosisScore,
        HepCTreatmentStatus txStatus,
        DateTime? txStartDate,
        DateTime? txEndDate,
        bool svrAchieved,
        DateTime? svrDate);

    Task UpdateDiabetesDataAsync(
        DiabetesType diabetesType,
        decimal? hbA1cPct,
        DateTime? hbA1cDate,
        bool isInsulinDependent,
        List<string> complications);

    /// <summary>Updates enriched diabetes fields: labs, BP, medications, exams, education.</summary>
    Task UpdateDiabetesEnrichedDataAsync(
        decimal? ldlMgDl, DateTime? ldlDate,
        decimal? microalbuminMgL, DateTime? microalbuminDate,
        int? bpSystolic, int? bpDiastolic, DateTime? bpDate,
        DiabetesMedicationStatus? medications,
        DiabetesExamRecord? exams,
        DiabetesEducationRecord? education);

    /// <summary>Updates asthma-specific registry data.</summary>
    Task UpdateAsthmaDataAsync(
        DateTime? diagnosisDate,
        AsthmaSeverity? severity,
        AsthmaControlLevel? controlLevel,
        DateTime? spirometryDate,
        decimal? fev1PredictedPct,
        decimal? fev1FvcRatio,
        int? peakFlowLPerMin,
        int? peakFlowPersonalBest,
        string? controllerMedication,
        string? rescueMedication,
        bool hasAsthmaActionPlan,
        List<string>? asthmaTriggers,
        int? asthmaEdVisitsLast12Months);
}
