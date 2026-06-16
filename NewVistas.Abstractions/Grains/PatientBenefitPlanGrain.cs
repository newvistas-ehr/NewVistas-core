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
/// Stores a patient's active insurance benefit plan.
/// Grain key: "PBM-PATIENT:{patientId}"
/// </summary>
public class PatientBenefitPlanGrain : Grain, IPatientBenefitPlanGrain
{
    private readonly IPersistentState<PatientBenefitPlanState> _state;

    public PatientBenefitPlanGrain(
        [PersistentState("patientBenefitState", "patientBenefitStore")]
        IPersistentState<PatientBenefitPlanState> state)
    {
        _state = state;
    }

    public Task<PatientBenefitPlanState> GetPlanAsync() =>
        Task.FromResult(_state.State);

    public async Task SetPlanAsync(string planId, string planName, string insuranceName,
        string? groupNumber, string? memberId, DateTime? effectiveDate,
        DateTime? terminationDate, decimal copayTier1, decimal copayTier2,
        decimal copayTier3, int daySupplyLimit, decimal annualDeductible)
    {
        PatientBenefitPlanState s = _state.State;
        s.PlanId = planId;
        s.PlanName = planName;
        s.InsuranceName = insuranceName;
        s.GroupNumber = groupNumber;
        s.MemberId = memberId;
        s.EffectiveDate = effectiveDate;
        s.TerminationDate = terminationDate;
        s.IsActive = true;
        s.CopayTier1 = copayTier1;
        s.CopayTier2 = copayTier2;
        s.CopayTier3 = copayTier3;
        s.DaySupplyLimit = daySupplyLimit;
        s.AnnualDeductible = annualDeductible;
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeductibleMetAsync()
    {
        _state.State.DeductibleMet = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
