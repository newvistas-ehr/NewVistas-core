// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class MeansTestBillingClockGrain : Grain, IMeansTestBillingClockGrain
{
    private readonly IPersistentState<MeansTestBillingClockState> _state;

    public MeansTestBillingClockGrain(
        [PersistentState("meansTestBillingClockState", "meansTestBillingClockStore")]
        IPersistentState<MeansTestBillingClockState> state)
    {
        _state = state;
    }

    public Task<MeansTestBillingClockState> GetAsync() => Task.FromResult(_state.State);

    public async Task SetClockAsync(
        string clockStatus,
        DateTime? startDate,
        DateTime? expirationDate,
        string? meansTestId,
        string? billingCategory,
        string? priorityGroup)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId   = this.GetPrimaryKeyString().Replace("IB-CLOCK:", string.Empty);
            _state.State.CreatedDate = DateTime.UtcNow;
        }

        _state.State.ClockStatus        = clockStatus;
        _state.State.ClockStartDate     = startDate;
        _state.State.ClockExpirationDate = expirationDate;
        _state.State.MeansTestId        = meansTestId;
        _state.State.BillingCategory    = billingCategory;
        _state.State.PriorityGroup      = priorityGroup;
        _state.State.SuspendedReason    = null;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResetClockAsync(string resetReason)
    {
        _state.State.LastResetDate    = DateTime.UtcNow;
        _state.State.ResetReason      = resetReason;
        _state.State.ClockStatus      = "NOT STARTED";
        _state.State.ClockStartDate   = null;
        _state.State.ClockExpirationDate = null;
        _state.State.SuspendedReason  = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SuspendClockAsync(string reason)
    {
        _state.State.ClockStatus      = "SUSPENDED";
        _state.State.SuspendedReason  = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
