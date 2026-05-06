// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARDebtorGrain : Grain, IARDebtorGrain
{
    private readonly IPersistentState<ARDebtorState> _state;

    public ARDebtorGrain(
        [PersistentState("arDebtorState", "arDebtorStore")]
        IPersistentState<ARDebtorState> state)
    {
        _state = state;
    }

    public Task<ARDebtorState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task EnsureInitializedAsync(string patientId, string name)
    {
        if (!string.IsNullOrEmpty(_state.State.DebtorId))
            return;

        _state.State.DebtorId         = this.GetPrimaryKeyString();
        _state.State.PatientId        = patientId;
        _state.State.Name             = name;
        _state.State.CreatedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateBalanceSummaryAsync(decimal totalAmountOwed, decimal totalAmountPaid, decimal currentBalance)
    {
        _state.State.TotalAmountOwed  = totalAmountOwed;
        _state.State.TotalAmountPaid  = totalAmountPaid;
        _state.State.CurrentBalance   = currentBalance;
        _state.State.LastActivityDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDelinquentAsync(DateTime delinquentSince)
    {
        _state.State.IsDelinquent     = true;
        _state.State.DelinquentSince  = delinquentSince;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReferToCollectionAsync(DateTime referralDate)
    {
        _state.State.IsInCollection          = true;
        _state.State.CollectionReferralDate  = referralDate;
        _state.State.LastModifiedDate        = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ClearDelinquencyAsync()
    {
        _state.State.IsDelinquent     = false;
        _state.State.IsInCollection   = false;
        _state.State.DelinquentSince  = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
