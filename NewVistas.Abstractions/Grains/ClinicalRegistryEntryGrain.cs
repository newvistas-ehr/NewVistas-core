// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ClinicalRegistryEntryGrain : Grain, IClinicalRegistryEntryGrain
{
    private readonly IPersistentState<ClinicalRegistryEntryState> _state;

    public ClinicalRegistryEntryGrain(
        [PersistentState("ccrEntryState", "ccrEntryStore")] IPersistentState<ClinicalRegistryEntryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EntryId))
            _state.State.EntryId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ClinicalRegistryEntryState> GetEntryAsync() => Task.FromResult(_state.State);

    public async Task EnrollPatientAsync(
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
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.RegistryType = registryType;
        _state.State.EnrolledById = enrolledById;
        _state.State.EnrolledByName = enrolledByName;
        _state.State.SiteId = siteId;
        _state.State.SiteName = siteName;
        _state.State.PrimaryProviderId = primaryProviderId;
        _state.State.PrimaryProviderName = primaryProviderName;
        _state.State.Notes = notes;
        _state.State.EnrollmentStatus = CCREnrollmentStatus.Active;
        _state.State.EnrollmentDate = DateTime.UtcNow;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateEnrollmentStatusAsync(CCREnrollmentStatus status, DateTime? deactivationDate, string? reason)
    {
        _state.State.EnrollmentStatus = status;
        _state.State.DeactivationDate = deactivationDate;
        _state.State.DeactivationReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateHIVDataAsync(
        HIVStage stage,
        decimal? cd4Count,
        DateTime? cd4Date,
        decimal? viralLoadCopies,
        DateTime? viralLoadDate,
        bool isVirallySuppressed,
        DateTime? artStartDate,
        string? artRegimen)
    {
        _state.State.HIVStage = stage;
        _state.State.CD4CountCellsPerMm3 = cd4Count;
        _state.State.CD4Date = cd4Date;
        _state.State.ViralLoadCopiesPerMl = viralLoadCopies;
        _state.State.ViralLoadDate = viralLoadDate;
        _state.State.IsVirallySuppressed = isVirallySuppressed;
        _state.State.ARTStartDate = artStartDate;
        _state.State.CurrentARTRegimen = artRegimen;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateHepCDataAsync(
        HepCGenotype genotype,
        decimal? fibrosisScore,
        HepCTreatmentStatus txStatus,
        DateTime? txStartDate,
        DateTime? txEndDate,
        bool svrAchieved,
        DateTime? svrDate)
    {
        _state.State.HepCGenotype = genotype;
        _state.State.FibrosisScoreKpa = fibrosisScore;
        _state.State.HepCTreatmentStatus = txStatus;
        _state.State.HepCTreatmentStartDate = txStartDate;
        _state.State.HepCTreatmentEndDate = txEndDate;
        _state.State.SVRAchieved = svrAchieved;
        _state.State.SVRDate = svrDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDiabetesDataAsync(
        DiabetesType diabetesType,
        decimal? hbA1cPct,
        DateTime? hbA1cDate,
        bool isInsulinDependent,
        List<string> complications)
    {
        _state.State.DiabetesType = diabetesType;
        _state.State.HbA1cPct = hbA1cPct;
        _state.State.HbA1cDate = hbA1cDate;
        _state.State.IsInsulinDependent = isInsulinDependent;
        _state.State.DiabetesComplications = complications;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDiabetesEnrichedDataAsync(
        decimal? ldlMgDl, DateTime? ldlDate,
        decimal? microalbuminMgL, DateTime? microalbuminDate,
        int? bpSystolic, int? bpDiastolic, DateTime? bpDate,
        DiabetesMedicationStatus? medications,
        DiabetesExamRecord? exams,
        DiabetesEducationRecord? education)
    {
        _state.State.LdlMgDl = ldlMgDl;
        _state.State.LdlDate = ldlDate;
        _state.State.MicroalbuminMgL = microalbuminMgL;
        _state.State.MicroalbuminDate = microalbuminDate;
        _state.State.BloodPressureSystolic = bpSystolic;
        _state.State.BloodPressureDiastolic = bpDiastolic;
        _state.State.BloodPressureDate = bpDate;
        _state.State.DiabetesMedications = medications;
        _state.State.DiabetesExams = exams;
        _state.State.DiabetesEducation = education;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateAsthmaDataAsync(
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
        int? asthmaEdVisitsLast12Months)
    {
        _state.State.AsthmaDiagnosisDate = diagnosisDate;
        _state.State.AsthmaSeverity = severity;
        _state.State.AsthmaControlLevel = controlLevel;
        _state.State.SpirometryDate = spirometryDate;
        _state.State.Fev1PredictedPct = fev1PredictedPct;
        _state.State.Fev1FvcRatio = fev1FvcRatio;
        _state.State.PeakFlowLPerMin = peakFlowLPerMin;
        _state.State.PeakFlowPersonalBest = peakFlowPersonalBest;
        _state.State.ControllerMedication = controllerMedication;
        _state.State.RescueMedication = rescueMedication;
        _state.State.HasAsthmaActionPlan = hasAsthmaActionPlan;
        _state.State.AsthmaTriggers = asthmaTriggers ?? new();
        _state.State.AsthmaEdVisitsLast12Months = asthmaEdVisitsLast12Months;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
