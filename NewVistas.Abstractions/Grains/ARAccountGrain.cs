// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Transactions.Abstractions;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// A single Accounts Receivable account (VistA File #430).
///
/// Money paths use Orleans <see cref="ITransactionalState{T}"/>: every method that
/// posts a financial transaction writes the <see cref="ARTransactionGrain"/> record
/// AND mutates this account's balance inside one ACID transaction. A silo crash (or
/// any exception) between the two writes rolls both back, so the transaction ledger
/// and the account balance can never disagree. The grain methods are decorated with
/// <c>[Transaction]</c> on <see cref="IARAccountGrain"/>; the transaction commits when
/// the method returns and is coordinated across the account and transaction grains.
/// </summary>
public class ARAccountGrain : Grain, IARAccountGrain
{
    private readonly ITransactionalState<ARAccountState> _state;

    public ARAccountGrain(
        [TransactionalState("arAccountState", "arAccountStore")]
        ITransactionalState<ARAccountState> state)
    {
        _state = state;
    }

    public Task<ARAccountState> GetAsync()
        => _state.PerformRead(s => s);

    public Task CreateAsync(
        string patientId,
        string? billingActionId,
        ARAccountCategory arCategory,
        decimal originalAmount,
        DateTime? dueDate)
    {
        string accountId = this.GetPrimaryKeyString();
        return _state.PerformUpdate(s =>
        {
            s.ARAccountId      = accountId;
            s.PatientId        = patientId;
            s.BillingActionId  = billingActionId;
            s.ARCategory       = arCategory;
            s.ARStatus         = ARAccountStatus.Active;
            s.OriginalAmount   = originalAmount;
            s.CurrentBalance   = originalAmount;
            s.DateEstablished  = DateTime.UtcNow;
            s.DueDate          = dueDate;
            s.CreatedDate      = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
        });
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
        string txnId = await RecordTransactionAsync(
            ARTransactionType.Payment, amount, appliedByUserId, appliedByUserName,
            receiptNumber, checkNumber, paymentMethod, null, notes);

        await _state.PerformUpdate(s =>
        {
            s.AmountPaid      += amount;
            s.CurrentBalance  -= amount;
            s.LastActivityDate = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
            if (s.CurrentBalance <= 0)
            {
                s.CurrentBalance = 0;
                s.ARStatus       = ARAccountStatus.Paid;
            }
        });
        return txnId;
    }

    public async Task<string> PostAdjustmentAsync(
        decimal amount,
        string adjustmentType,
        string appliedByUserId,
        string appliedByUserName,
        string? notes)
    {
        string txnId = await RecordTransactionAsync(
            ARTransactionType.Adjustment, amount, appliedByUserId, appliedByUserName,
            null, null, null, adjustmentType, notes);

        await _state.PerformUpdate(s =>
        {
            s.CurrentBalance  -= amount;
            s.LastActivityDate = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
            if (s.CurrentBalance <= 0)
            {
                s.CurrentBalance = 0;
                s.ARStatus       = ARAccountStatus.Paid;
            }
        });
        return txnId;
    }

    public async Task<string> WaiveAsync(
        decimal waivedAmount,
        string waivedByUserId,
        string waivedByUserName,
        string reason)
    {
        string txnId = await RecordTransactionAsync(
            ARTransactionType.Waiver, waivedAmount, waivedByUserId, waivedByUserName,
            null, null, null, null, reason);

        await _state.PerformUpdate(s =>
        {
            s.AmountWaived    += waivedAmount;
            s.CurrentBalance  -= waivedAmount;
            s.LastActivityDate = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
            if (s.CurrentBalance <= 0)
            {
                s.CurrentBalance = 0;
                s.ARStatus       = ARAccountStatus.Waived;
            }
        });
        return txnId;
    }

    public async Task<string> WriteOffAsync(
        decimal writeOffAmount,
        string writtenOffByUserId,
        string writtenOffByUserName,
        string reason)
    {
        string txnId = await RecordTransactionAsync(
            ARTransactionType.WriteOff, writeOffAmount, writtenOffByUserId, writtenOffByUserName,
            null, null, null, null, reason);

        await _state.PerformUpdate(s =>
        {
            s.CurrentBalance  -= writeOffAmount;
            s.LastActivityDate = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
            s.ARStatus         = ARAccountStatus.WrittenOff;
            if (s.CurrentBalance < 0)
                s.CurrentBalance = 0;
        });
        return txnId;
    }

    public async Task<string> AccrueInterestAsync(decimal interestAmount, string appliedByUserId)
    {
        string txnId = await RecordTransactionAsync(
            ARTransactionType.Interest, interestAmount, appliedByUserId, "System",
            null, null, null, null, null);

        await _state.PerformUpdate(s =>
        {
            s.InterestAccrued += interestAmount;
            s.CurrentBalance  += interestAmount;
            s.LastActivityDate = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
        });
        return txnId;
    }

    public async Task<string> AccruePenaltyAsync(decimal penaltyAmount, string appliedByUserId)
    {
        string txnId = await RecordTransactionAsync(
            ARTransactionType.Penalty, penaltyAmount, appliedByUserId, "System",
            null, null, null, null, null);

        await _state.PerformUpdate(s =>
        {
            s.PenaltyAccrued  += penaltyAmount;
            s.CurrentBalance  += penaltyAmount;
            s.LastActivityDate = DateTime.UtcNow;
            s.LastModifiedDate = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
        });
        return txnId;
    }

    public async Task<string> AccrueAdminCostAsync(decimal adminCostAmount, string appliedByUserId)
    {
        string txnId = await RecordTransactionAsync(
            ARTransactionType.AdminCost, adminCostAmount, appliedByUserId, "System",
            null, null, null, null, null);

        await _state.PerformUpdate(s =>
        {
            s.AdminCostAccrued += adminCostAmount;
            s.CurrentBalance   += adminCostAmount;
            s.LastActivityDate  = DateTime.UtcNow;
            s.LastModifiedDate  = DateTime.UtcNow;
            s.TransactionIds.Add(txnId);
        });
        return txnId;
    }

    public Task ReferToTopAsync(string referralId, decimal referredAmount, string referredByUserId, string referredByUserName)
        => _state.PerformUpdate(s =>
        {
            s.IsTreasuryOffset = true;
            s.ARStatus         = ARAccountStatus.TreasuryOffset;
            s.LastModifiedDate = DateTime.UtcNow;
        });

    public async Task RecordTopOffsetAsync(decimal offsetAmount, string processedByUserId, string processedByUserName)
    {
        // Runs inside this method's ambient transaction; the payment record, the
        // balance reduction, and the status change below all commit atomically.
        await PostPaymentAsync(offsetAmount, "TOP", processedByUserId, processedByUserName, null, null, "Treasury Offset payment");

        decimal balance = await _state.PerformRead(s => s.CurrentBalance);
        if (balance <= 0)
        {
            await _state.PerformUpdate(s =>
            {
                s.IsTreasuryOffset = false;
                s.ARStatus         = ARAccountStatus.Paid;
                s.LastModifiedDate = DateTime.UtcNow;
            });
        }
    }

    public Task WithdrawTopReferralAsync()
        => _state.PerformUpdate(s =>
        {
            s.IsTreasuryOffset = false;
            s.ARStatus         = ARAccountStatus.Active;
            s.LastModifiedDate = DateTime.UtcNow;
        });

    public Task UpdateStatusAsync(ARAccountStatus status)
        => _state.PerformUpdate(s =>
        {
            s.ARStatus         = status;
            s.LastModifiedDate = DateTime.UtcNow;
        });

    /// <summary>
    /// Writes a new <see cref="ARTransactionGrain"/> record for this account inside the
    /// caller's ambient transaction and returns its id. The caller then mutates the
    /// account balance in the same transaction, so both persist or neither does.
    /// </summary>
    private async Task<string> RecordTransactionAsync(
        ARTransactionType transactionType,
        decimal amount,
        string appliedByUserId,
        string appliedByUserName,
        string? receiptNumber,
        string? checkNumber,
        string? paymentMethod,
        string? referenceNumber,
        string? notes)
    {
        string txnId = Guid.NewGuid().ToString();
        (string accountId, string patientId) = await _state.PerformRead(s => (s.ARAccountId, s.PatientId));

        IARTransactionGrain txn = GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");
        await txn.CreateAsync(
            accountId, patientId, transactionType, amount,
            appliedByUserId, appliedByUserName,
            receiptNumber, checkNumber, paymentMethod, referenceNumber, notes);
        return txnId;
    }
}
