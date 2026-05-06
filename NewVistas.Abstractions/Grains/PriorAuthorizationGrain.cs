// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Manages the lifecycle of a single prior-authorization request.
/// Grain key: "PA:{guid}"
/// </summary>
public class PriorAuthorizationGrain : Grain, IPriorAuthorizationGrain
{
    private readonly IPersistentState<PriorAuthorizationState> _state;

    public PriorAuthorizationGrain(
        [PersistentState("priorAuthState", "priorAuthStore")]
        IPersistentState<PriorAuthorizationState> state)
    {
        _state = state;
    }

    public Task<PriorAuthorizationState> GetAsync() =>
        Task.FromResult(_state.State);

    public async Task SubmitRequestAsync(string patientId, string? drugId, string drugName,
        string? planId, string? providerId, string? providerName,
        List<string> diagnosisCodes, string? clinicalJustification)
    {
        PriorAuthorizationState s = _state.State;
        s.PaId = this.GetPrimaryKeyString();
        s.PatientId = patientId;
        s.DrugId = drugId;
        s.DrugName = drugName;
        s.PlanId = planId;
        s.ProviderId = providerId;
        s.ProviderName = providerName;
        s.Status = "PENDING";
        s.RequestDate = DateTime.UtcNow;
        s.DiagnosisCodes = diagnosisCodes;
        s.ClinicalJustification = clinicalJustification;
        s.CreatedDate = DateTime.UtcNow;
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(string reviewerId, string reviewerName,
        string? notes, DateTime? expirationDate)
    {
        PriorAuthorizationState s = _state.State;
        s.Status = "APPROVED";
        s.ReviewDate = DateTime.UtcNow;
        s.DecisionById = reviewerId;
        s.DecisionByName = reviewerName;
        s.DecisionNotes = notes;
        s.ExpirationDate = expirationDate;
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DenyAsync(string reviewerId, string reviewerName, string reason)
    {
        PriorAuthorizationState s = _state.State;
        s.Status = "DENIED";
        s.ReviewDate = DateTime.UtcNow;
        s.DecisionById = reviewerId;
        s.DecisionByName = reviewerName;
        s.DenialReason = reason;
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ExpireAsync()
    {
        _state.State.Status = "EXPIRED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync()
    {
        _state.State.Status = "CANCELLED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
