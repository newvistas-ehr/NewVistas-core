// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IBillingPatientGrain : Grain, IIBillingPatientGrain
{
    private readonly IPersistentState<IBillingPatientState> _state;

    public IBillingPatientGrain(
        [PersistentState("ibBillingPatientState", "ibBillingPatientStore")]
        IPersistentState<IBillingPatientState> state)
    {
        _state = state;
    }

    public Task<IBillingPatientState> GetAsync() => Task.FromResult(_state.State);

    public async Task EnsureInitializedAsync(string patientId)
    {
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId       = patientId;
        _state.State.CopayAccountId  = Guid.NewGuid().ToString();
        _state.State.CreatedDate     = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCopayExemptionAsync(
        bool isExempt,
        string? reasonCode,
        DateTime? effectiveDate,
        DateTime? expirationDate)
    {
        _state.State.IsExemptFromCopay        = isExempt;
        _state.State.ExemptionReasonCode      = isExempt ? reasonCode : null;
        _state.State.ExemptionEffectiveDate   = isExempt ? effectiveDate : null;
        _state.State.ExemptionExpirationDate  = isExempt ? expirationDate : null;
        _state.State.LastModifiedDate         = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkCopayCapReachedAsync(DateTime capReachedDate)
    {
        _state.State.CopayCapReachedDate = capReachedDate;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task GrantHardshipAsync(DateTime grantedDate, DateTime? expirationDate)
    {
        _state.State.IsHardshipGranted    = true;
        _state.State.HardshipGrantedDate  = grantedDate;
        _state.State.HardshipExpirationDate = expirationDate;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCopayTransactionAsync(
        string billingActionId,
        string actionTypeDescription,
        decimal amount,
        DateTime serviceDate,
        bool isExempt)
    {
        if (!isExempt)
            _state.State.CurrentYearCopayBalance += amount;

        _state.State.YearToDateCopayTransactions.Insert(0, new CopayTransactionSummary
        {
            BillingActionId = billingActionId,
            Description     = actionTypeDescription,
            Amount          = amount,
            ServiceDate     = serviceDate,
            IsExempt        = isExempt,
        });

        // Keep rolling history to last 100 entries
        if (_state.State.YearToDateCopayTransactions.Count > 100)
            _state.State.YearToDateCopayTransactions.RemoveRange(100,
                _state.State.YearToDateCopayTransactions.Count - 100);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
