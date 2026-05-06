// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ControlPointGrain : Grain, IControlPointGrain
{
    private readonly IPersistentState<ControlPointState> _state;

    public ControlPointGrain(
        [PersistentState("controlPointState", "ifcapControlPointStore")]
        IPersistentState<ControlPointState> state)
    {
        _state = state;
    }

    public Task<ControlPointState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string name,
        string facilityId,
        string serviceId,
        int fiscalYear,
        string budgetCode,
        decimal allocatedAmount,
        string officerId,
        string officerName)
    {
        _state.State.ControlPointId          = this.GetPrimaryKeyString();
        _state.State.Name                    = name;
        _state.State.FacilityId              = facilityId;
        _state.State.ServiceId               = serviceId;
        _state.State.FiscalYear              = fiscalYear;
        _state.State.BudgetCode              = budgetCode;
        _state.State.AllocatedAmount         = allocatedAmount;
        _state.State.RemainingBalance        = allocatedAmount;
        _state.State.ObligatedAmount         = 0m;
        _state.State.ExpendedAmount          = 0m;
        _state.State.Status                  = ControlPointStatus.Active;
        _state.State.ControlPointOfficerId   = officerId;
        _state.State.ControlPointOfficerName = officerName;
        _state.State.CreatedDate             = DateTime.UtcNow;
        _state.State.LastModifiedDate        = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AllocateFundsAsync(decimal amount, string authorizedByUserId)
    {
        _state.State.AllocatedAmount  += amount;
        _state.State.RemainingBalance += amount;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ObligateFundsAsync(decimal amount, string requestId)
    {
        _state.State.RemainingBalance -= amount;
        _state.State.ObligatedAmount  += amount;
        if (!_state.State.RequestIds.Contains(requestId))
            _state.State.RequestIds.Add(requestId);
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ExpenditureAsync(decimal amount, string poId)
    {
        _state.State.ObligatedAmount  -= amount;
        _state.State.ExpendedAmount   += amount;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(ControlPointStatus status)
    {
        _state.State.Status           = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
