// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARAccountGrain : Grain, IARAccountGrain
{
    private readonly IPersistentState<ARAccountState> _state;

    public ARAccountGrain(
        [PersistentState("arAccountState", "arAccountStore")]
        IPersistentState<ARAccountState> state)
    {
        _state = state;
    }

    public Task<ARAccountState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        string? billingActionId,
        ARAccountCategory arCategory,
        decimal originalAmount,
        DateTime? dueDate)
    {
        _state.State.ARAccountId      = this.GetPrimaryKeyString();
        _state.State.PatientId        = patientId;
        _state.State.BillingActionId  = billingActionId;
        _state.State.ARCategory       = arCategory;
        _state.State.ARStatus         = ARAccountStatus.Active;
        _state.State.OriginalAmount   = originalAmount;
        _state.State.CurrentBalance   = originalAmount;
        _state.State.DateEstablished  = DateTime.UtcNow;
        _state.State.DueDate          = dueDate;
        _state.State.CreatedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<string> PostPaymentAsync(
        decimal amount,
        string paymentMethod,
        string appliedByUserId,
        string appliedByUserName,
        string? receiptNumber,
        string? checkNumber,
        string? notes)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.Payment, amount,
            appliedByUserId, appliedByUserName,
            receiptNumber, checkNumber, paymentMethod, null, notes);

        _state.State.AmountPaid       += amount;
        _state.State.CurrentBalance   -= amount;
        _state.State.LastActivityDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);

        if (_state.State.CurrentBalance <= 0)
        {
            _state.State.CurrentBalance = 0;
            _state.State.ARStatus       = ARAccountStatus.Paid;
        }

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task<string> PostAdjustmentAsync(
        decimal amount,
        string adjustmentType,
        string appliedByUserId,
        string appliedByUserName,
        string? notes)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.Adjustment, amount,
            appliedByUserId, appliedByUserName,
            null, null, null, adjustmentType, notes);

        _state.State.CurrentBalance   -= amount;
        _state.State.LastActivityDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);

        if (_state.State.CurrentBalance <= 0)
        {
            _state.State.CurrentBalance = 0;
            _state.State.ARStatus       = ARAccountStatus.Paid;
        }

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task<string> WaiveAsync(
        decimal waivedAmount,
        string waivedByUserId,
        string waivedByUserName,
        string reason)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.Waiver, waivedAmount,
            waivedByUserId, waivedByUserName,
            null, null, null, null, reason);

        _state.State.AmountWaived     += waivedAmount;
        _state.State.CurrentBalance   -= waivedAmount;
        _state.State.LastActivityDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);

        if (_state.State.CurrentBalance <= 0)
        {
            _state.State.CurrentBalance = 0;
            _state.State.ARStatus       = ARAccountStatus.Waived;
        }

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task<string> WriteOffAsync(
        decimal writeOffAmount,
        string writtenOffByUserId,
        string writtenOffByUserName,
        string reason)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.WriteOff, writeOffAmount,
            writtenOffByUserId, writtenOffByUserName,
            null, null, null, null, reason);

        _state.State.CurrentBalance   -= writeOffAmount;
        _state.State.LastActivityDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);
        _state.State.ARStatus          = ARAccountStatus.WrittenOff;

        if (_state.State.CurrentBalance < 0)
            _state.State.CurrentBalance = 0;

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task<string> AccrueInterestAsync(decimal interestAmount, string appliedByUserId)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.Interest, interestAmount,
            appliedByUserId, "System",
            null, null, null, null, null);

        _state.State.InterestAccrued  += interestAmount;
        _state.State.CurrentBalance   += interestAmount;
        _state.State.LastActivityDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task<string> AccruePenaltyAsync(decimal penaltyAmount, string appliedByUserId)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.Penalty, penaltyAmount,
            appliedByUserId, "System",
            null, null, null, null, null);

        _state.State.PenaltyAccrued   += penaltyAmount;
        _state.State.CurrentBalance   += penaltyAmount;
        _state.State.LastActivityDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task<string> AccrueAdminCostAsync(decimal adminCostAmount, string appliedByUserId)
    {
        string txnId = Guid.NewGuid().ToString();
        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            _state.State.ARAccountId, _state.State.PatientId,
            ARTransactionType.AdminCost, adminCostAmount,
            appliedByUserId, "System",
            null, null, null, null, null);

        _state.State.AdminCostAccrued  += adminCostAmount;
        _state.State.CurrentBalance    += adminCostAmount;
        _state.State.LastActivityDate   = DateTime.UtcNow;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        _state.State.TransactionIds.Add(txnId);

        await _state.WriteStateAsync();
        return txnId;
    }

    public async Task ReferToTopAsync(string referralId, decimal referredAmount, string referredByUserId, string referredByUserName)
    {
        _state.State.IsTreasuryOffset  = true;
        _state.State.ARStatus          = ARAccountStatus.TreasuryOffset;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordTopOffsetAsync(decimal offsetAmount, string processedByUserId, string processedByUserName)
    {
        await PostPaymentAsync(offsetAmount, "TOP", processedByUserId, processedByUserName, null, null, "Treasury Offset payment");

        if (_state.State.CurrentBalance <= 0)
        {
            _state.State.IsTreasuryOffset = false;
            _state.State.ARStatus         = ARAccountStatus.Paid;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task WithdrawTopReferralAsync()
    {
        _state.State.IsTreasuryOffset  = false;
        _state.State.ARStatus          = ARAccountStatus.Active;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(ARAccountStatus status)
    {
        _state.State.ARStatus         = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
