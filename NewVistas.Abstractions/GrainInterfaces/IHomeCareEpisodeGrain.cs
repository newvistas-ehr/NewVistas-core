// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A single home-care episode — the spine of the Home-Based Care module.
/// Key pattern: "HHC-EPISODE:{guid}". VistA File #750 (HOME BASED PRIMARY CARE). HBPC.m
/// </summary>
public interface IHomeCareEpisodeGrain : IGrainWithStringKey
{
    /// <summary>Admits the patient to the home-care program and opens the episode.</summary>
    Task AdmitAsync(
        string patientId,
        string patientName,
        HomeCareProgramType programType,
        DateTime admissionDate,
        HomeCareAdmissionSource admissionSource,
        string referringProviderId,
        string referringProviderName,
        string primaryDiagnosisCode,
        string primaryDiagnosisText,
        HomeCareLevelOfCare levelOfCare,
        string clinicalNeedNarrative,
        string primaryCaregiver,
        string homeAddress);

    Task UpdateLevelOfCareAsync(HomeCareLevelOfCare levelOfCare);
    Task UpdateEligibilityAsync(HomeCareEligibility eligibility);
    Task AddSecondaryDiagnosisAsync(string diagnosis);
    Task AddTeamMemberAsync(HomeCareTeamMember member);
    Task RemoveTeamMemberAsync(string providerId);
    Task SetPlanOfCareIdAsync(string planId);

    /// <summary>Links a visit id to the episode and refreshes last/next visit dates.</summary>
    Task AddVisitIdAsync(string visitId);
    Task AddAssessmentIdAsync(string assessmentId);

    /// <summary>Updates the episode's last-completed / next-scheduled visit dates.</summary>
    Task RecordVisitDatesAsync(DateTime? lastVisitDate, DateTime? nextVisitDate);

    Task PutOnHoldAsync(string reason);
    Task ReactivateAsync();
    Task DischargeAsync(DateTime dischargeDate, HomeCareDischargeReason reason, string notes);
    Task MarkDeceasedAsync(DateTime date, string notes);

    // ── Medicare (Phase 2): certification periods ──
    /// <summary>Opens a 60-day certification period (with its 30-day payment periods) on the episode.</summary>
    Task OpenCertificationPeriodAsync(CertificationPeriod period);

    /// <summary>Stores the PDGM grouping result on a payment period within a certification period.</summary>
    Task SetPaymentPeriodGroupingAsync(string certificationPeriodId, string paymentPeriodId, PdgmGroupingResult grouping);

    Task<HomeCareEpisodeState> GetEpisodeAsync();
}
