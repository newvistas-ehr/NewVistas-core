// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PatientRiskGrain : Grain, IPatientRiskGrain
{
    private readonly IPersistentState<PatientRiskState> _state;

    public PatientRiskGrain(
        [PersistentState("spRiskState", "spRiskStore")] IPersistentState<PatientRiskState> state)
    {
        _state = state;
    }

    public Task<PatientRiskState> GetRiskStateAsync() =>
        Task.FromResult(_state.State);

    public async Task SetRiskLevelAsync(RiskLevel level, string patientId, string patientName, string providerId, string providerName)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.CurrentRiskLevel = level;
        _state.State.DesignationHistory.Add(new RiskDesignationEntry
        {
            DesignatedDate = DateTime.UtcNow,
            RiskLevel = level,
            ProviderId = providerId,
            ProviderName = providerName,
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetHighRiskFlagAsync(bool flagged)
    {
        _state.State.IsHighRiskFlagged = flagged;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddFollowUpContactAsync(FollowUpContact contact)
    {
        contact.ContactId = Guid.NewGuid().ToString();
        _state.State.FollowUpContacts.Add(contact);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
