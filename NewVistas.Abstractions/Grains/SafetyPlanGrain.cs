// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class SafetyPlanGrain : Grain, ISafetyPlanGrain
{
    private readonly IPersistentState<SafetyPlanState> _state;

    public SafetyPlanGrain(
        [PersistentState("spPlanState", "spPlanStore")] IPersistentState<SafetyPlanState> state)
    {
        _state = state;
    }

    public Task<SafetyPlanState> GetPlanAsync() =>
        Task.FromResult(_state.State);

    public async Task CreatePlanAsync(string planId, string patientId, string patientName, string providerId, string providerName)
    {
        _state.State.PlanId = planId;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Status = SafetyPlanStatus.Draft;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateWarningSigns(List<string> signs)
    {
        _state.State.WarningSigns = signs ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateCopingStrategies(List<string> strategies)
    {
        _state.State.InternalCopingStrategies = strategies ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateContacts(
        List<string> distractionContacts,
        List<SupportContact> supportContacts,
        List<ProfessionalContact> professionalContacts,
        List<string> crisisLineNumbers)
    {
        _state.State.DistractionContacts = distractionContacts ?? new();
        _state.State.SupportContacts = supportContacts ?? new();
        _state.State.ProfessionalContacts = professionalContacts ?? new();
        _state.State.CrisisLineNumbers = crisisLineNumbers ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateMeansRestriction(List<string> meansRemoved, string notes)
    {
        _state.State.MeansRemoved = meansRemoved ?? new();
        _state.State.EnvironmentSafetyNotes = notes ?? string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateReasonsForLiving(List<string> reasons)
    {
        _state.State.ReasonsForLiving = reasons ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReviewPlanAsync(DateTime reviewDate)
    {
        _state.State.LastReviewedDate = reviewDate;
        if (_state.State.Status == SafetyPlanStatus.Active)
            _state.State.Status = SafetyPlanStatus.Updated;
        else if (_state.State.Status == SafetyPlanStatus.Draft)
            _state.State.Status = SafetyPlanStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ArchivePlanAsync()
    {
        _state.State.Status = SafetyPlanStatus.Archived;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
