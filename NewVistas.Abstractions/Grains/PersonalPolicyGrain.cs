// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PersonalPolicyGrain : Grain, IPersonalPolicyGrain
{
    private readonly IPersistentState<PersonalPolicyState> _state;

    public PersonalPolicyGrain(
        [PersistentState("personalPolicyState", "personalPolicyStore")]
        IPersistentState<PersonalPolicyState> state)
    {
        _state = state;
    }

    public Task<PersonalPolicyState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId,
        string? groupPlanId,
        string groupPlanName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        string? coverageType,
        bool isPrimary,
        decimal? copayAmount,
        string? pharmacyMemberId,
        string? notes)
    {
        string policyId = this.GetPrimaryKeyString().Replace("IB-POLICY:", string.Empty);

        _state.State.PolicyId                = policyId;
        _state.State.PatientId               = patientId;
        _state.State.GroupPlanId             = groupPlanId;
        _state.State.GroupPlanName           = groupPlanName;
        _state.State.SubscriberId            = subscriberId;
        _state.State.SubscriberName          = subscriberName;
        _state.State.RelationshipToSubscriber = relationshipToSubscriber;
        _state.State.EffectiveDate           = effectiveDate;
        _state.State.ExpirationDate          = expirationDate;
        _state.State.CoverageType            = coverageType;
        _state.State.IsPrimary               = isPrimary;
        _state.State.CopayAmount             = copayAmount;
        _state.State.PharmacyMemberId        = pharmacyMemberId;
        _state.State.Notes                   = notes;
        _state.State.IsActive                = true;
        _state.State.CreatedDate             = DateTime.UtcNow;
        _state.State.LastModifiedDate        = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return policyId;
    }

    public async Task MarkDeductibleMetAsync(DateTime metDate)
    {
        _state.State.DeductibleMet     = true;
        _state.State.DeductibleMetDate = metDate;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive         = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCobraAsync(DateTime startDate, DateTime? endDate)
    {
        _state.State.CobraFlag        = true;
        _state.State.CobraStartDate   = startDate;
        _state.State.CobraEndDate     = endDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
