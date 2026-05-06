// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PayerConfigGrain : Grain, IPayerConfigGrain
{
    private readonly IPersistentState<PayerConfigState> _state;

    public PayerConfigGrain(
        [PersistentState("payerConfigState", "payerConfigStore")]
        IPersistentState<PayerConfigState> state)
    {
        _state = state;
    }

    public Task<PayerConfigState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string payerName,
        bool supportsRealTimeEligibility,
        string? interchangeReceiverId,
        string? interchangeReceiverQualifier,
        string? eligibilityEndpoint,
        string? eligibilityPhone,
        List<string>? supportedServiceTypeCodes,
        string? notes)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.PayerId                       = this.GetPrimaryKeyString();
        _state.State.PayerName                     = payerName;
        _state.State.SupportsRealTimeEligibility   = supportsRealTimeEligibility;
        _state.State.InterchangeReceiverId         = interchangeReceiverId;
        _state.State.InterchangeReceiverQualifier  = interchangeReceiverQualifier;
        _state.State.EligibilityEndpoint           = eligibilityEndpoint;
        _state.State.EligibilityPhone              = eligibilityPhone;
        _state.State.Notes                         = notes;
        _state.State.IsActive                      = true;
        _state.State.CreatedDate                   = now;
        _state.State.LastModifiedDate              = now;

        if (supportedServiceTypeCodes is not null)
            _state.State.SupportedServiceTypeCodes = supportedServiceTypeCodes;

        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive         = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive         = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
