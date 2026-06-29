// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeCareEpisodeGrain : Grain, IHomeCareEpisodeGrain
{
    private readonly IPersistentState<HomeCareEpisodeState> _state;

    public HomeCareEpisodeGrain(
        [PersistentState("homeCareEpisodeState", "homeCareEpisodeStore")] IPersistentState<HomeCareEpisodeState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EpisodeId))
        {
            _state.State.EpisodeId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AdmitAsync(
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
        string homeAddress)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ProgramType = programType;
        _state.State.AdmissionDate = admissionDate;
        _state.State.AdmissionSource = admissionSource;
        _state.State.ReferringProviderId = referringProviderId;
        _state.State.ReferringProviderName = referringProviderName;
        _state.State.PrimaryDiagnosisCode = primaryDiagnosisCode;
        _state.State.PrimaryDiagnosisText = primaryDiagnosisText;
        _state.State.LevelOfCare = levelOfCare;
        _state.State.Eligibility.ClinicalNeedNarrative = clinicalNeedNarrative;
        _state.State.PrimaryCaregiver = primaryCaregiver;
        _state.State.HomeAddress = homeAddress;
        _state.State.Status = HomeCareEpisodeStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateLevelOfCareAsync(HomeCareLevelOfCare levelOfCare)
    {
        _state.State.LevelOfCare = levelOfCare;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateEligibilityAsync(HomeCareEligibility eligibility)
    {
        _state.State.Eligibility = eligibility;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSecondaryDiagnosisAsync(string diagnosis)
    {
        if (!string.IsNullOrWhiteSpace(diagnosis) && !_state.State.SecondaryDiagnoses.Contains(diagnosis))
            _state.State.SecondaryDiagnoses.Add(diagnosis);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTeamMemberAsync(HomeCareTeamMember member)
    {
        if (member.AssignedDate == default) member.AssignedDate = DateTime.UtcNow;
        // Upsert by provider id — one current assignment per provider.
        _state.State.Team.RemoveAll(m => m.ProviderId == member.ProviderId);
        if (member.IsPrimary)
            foreach (HomeCareTeamMember m in _state.State.Team) m.IsPrimary = false;
        _state.State.Team.Add(member);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTeamMemberAsync(string providerId)
    {
        _state.State.Team.RemoveAll(m => m.ProviderId == providerId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPlanOfCareIdAsync(string planId)
    {
        _state.State.PlanOfCareId = planId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddVisitIdAsync(string visitId)
    {
        if (!_state.State.VisitIds.Contains(visitId))
            _state.State.VisitIds.Add(visitId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAssessmentIdAsync(string assessmentId)
    {
        if (!_state.State.AssessmentIds.Contains(assessmentId))
            _state.State.AssessmentIds.Add(assessmentId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordVisitDatesAsync(DateTime? lastVisitDate, DateTime? nextVisitDate)
    {
        if (lastVisitDate.HasValue) _state.State.LastVisitDate = lastVisitDate;
        _state.State.NextVisitDate = nextVisitDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PutOnHoldAsync(string reason)
    {
        _state.State.Status = HomeCareEpisodeStatus.OnHold;
        _state.State.OnHoldReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        _state.State.Status = HomeCareEpisodeStatus.Active;
        _state.State.OnHoldReason = string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargeAsync(DateTime dischargeDate, HomeCareDischargeReason reason, string notes)
    {
        _state.State.Status = HomeCareEpisodeStatus.Discharged;
        _state.State.DischargeDate = dischargeDate;
        _state.State.DischargeReason = reason;
        _state.State.DischargeNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeceasedAsync(DateTime date, string notes)
    {
        _state.State.Status = HomeCareEpisodeStatus.Deceased;
        _state.State.DischargeDate = date;
        _state.State.DischargeReason = HomeCareDischargeReason.Deceased;
        _state.State.DischargeNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task OpenCertificationPeriodAsync(CertificationPeriod period)
    {
        _state.State.CertificationPeriods.Add(period);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPaymentPeriodGroupingAsync(string certificationPeriodId, string paymentPeriodId, PdgmGroupingResult grouping)
    {
        CertificationPeriod? cert = _state.State.CertificationPeriods.FirstOrDefault(c => c.PeriodId == certificationPeriodId);
        PaymentPeriod? pp = cert?.PaymentPeriods.FirstOrDefault(p => p.PeriodId == paymentPeriodId);
        if (pp is null) return;
        pp.Grouping = grouping;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HomeCareEpisodeState> GetEpisodeAsync() => Task.FromResult(_state.State);
}
