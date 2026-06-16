// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Social Work Assessment Grain — VistA File #707 (SOCIAL WORK ASSESSMENT).
/// Key: "SW-ASSESSMENT:{guid}"
/// </summary>
public class SocialWorkAssessmentGrain : Grain, ISocialWorkAssessmentGrain
{
    private readonly IPersistentState<SocialWorkAssessmentState> _state;

    public SocialWorkAssessmentGrain(
        [PersistentState("socialWorkAssessmentState", "socialWorkAssessmentStore")]
        IPersistentState<SocialWorkAssessmentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AssessmentId))
        {
            _state.State.AssessmentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<SocialWorkAssessmentState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        SocialWorkAssessmentType assessmentType,
        DateTime assessmentDate,
        string? socialWorkerId,
        string? socialWorkerName,
        SocialWorkRiskLevel riskLevel,
        string? housingStatus,
        string? employmentStatus,
        string? socialSupport,
        string? financialStressors,
        string? substanceUseHistory,
        bool? abuseConcernsIdentified,
        bool? safetyPlanInPlace,
        DateTime? anticipatedDischargeDate,
        string? dischargeDisposition,
        string? dischargePlan,
        List<string>? dischargeBarriers,
        string? recommendations,
        string? notes,
        string? locationId,
        string? locationName)
    {
        _state.State.PatientId = patientId;
        _state.State.AssessmentType = assessmentType;
        _state.State.AssessmentDate = assessmentDate;
        _state.State.SocialWorkerId = socialWorkerId;
        _state.State.SocialWorkerName = socialWorkerName;
        _state.State.RiskLevel = riskLevel;
        _state.State.HousingStatus = housingStatus;
        _state.State.EmploymentStatus = employmentStatus;
        _state.State.SocialSupport = socialSupport;
        _state.State.FinancialStressors = financialStressors;
        _state.State.SubstanceUseHistory = substanceUseHistory;
        _state.State.AbuseConcernsIdentified = abuseConcernsIdentified;
        _state.State.SafetyPlanInPlace = safetyPlanInPlace;
        _state.State.AnticipatedDischargeDate = anticipatedDischargeDate;
        _state.State.DischargeDisposition = dischargeDisposition;
        _state.State.DischargePlan = dischargePlan;
        if (dischargeBarriers != null)
            _state.State.DischargeBarriers = dischargeBarriers;
        _state.State.Recommendations = recommendations;
        _state.State.Notes = notes;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Status = SocialWorkAssessmentStatus.Draft;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime completedDate, string? recommendations, string? notes)
    {
        _state.State.Status = SocialWorkAssessmentStatus.Complete;
        _state.State.CompletedDate = completedDate;
        if (recommendations != null)
            _state.State.Recommendations = recommendations;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseAsync(string reason)
    {
        _state.State.Status = SocialWorkAssessmentStatus.Closed;
        _state.State.ClosedReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateRiskLevelAsync(SocialWorkRiskLevel riskLevel)
    {
        _state.State.RiskLevel = riskLevel;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
