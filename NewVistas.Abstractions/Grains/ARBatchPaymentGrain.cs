// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARBatchPaymentGrain : Grain, IARBatchPaymentGrain
{
    private readonly IPersistentState<ARBatchPaymentState> _state;

    public ARBatchPaymentGrain(
        [PersistentState("arBatchPaymentState", "arBatchPaymentStore")]
        IPersistentState<ARBatchPaymentState> state)
    {
        _state = state;
    }

    public Task<ARBatchPaymentState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string? facilityId,
        string? facilityName,
        DateTime batchDate,
        string paymentMethod,
        string? checkNumber,
        string? notes)
    {
        _state.State.BatchId          = this.GetPrimaryKeyString();
        _state.State.FacilityId       = facilityId;
        _state.State.FacilityName     = facilityName;
        _state.State.BatchDate        = batchDate;
        _state.State.PaymentMethod    = paymentMethod;
        _state.State.CheckNumber      = checkNumber;
        _state.State.Notes            = notes;
        _state.State.CreatedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddPaymentAsync(
        string arAccountId,
        string patientId,
        string patientName,
        decimal amount,
        string? receiptNumber)
    {
        ARBatchPaymentEntry entry = new()
        {
            ARAccountId   = arAccountId,
            PatientId     = patientId,
            PatientName   = patientName,
            Amount        = amount,
            ReceiptNumber = receiptNumber,
        };

        _state.State.Payments.Add(entry);
        _state.State.TotalAmount      += amount;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PostAsync(string postedByUserId, string postedByUserName)
    {
        foreach (ARBatchPaymentEntry payment in _state.State.Payments)
        {
            IARAccountGrain account = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{payment.ARAccountId}");
            await account.PostPaymentAsync(
                payment.Amount,
                _state.State.PaymentMethod,
                postedByUserId,
                postedByUserName,
                payment.ReceiptNumber,
                _state.State.CheckNumber,
                $"Batch payment {_state.State.BatchId}");
        }

        _state.State.IsPosted         = true;
        _state.State.PostedByUserId   = postedByUserId;
        _state.State.PostedDate       = DateTime.UtcNow;
        _state.State.ProcessedDate    = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        IARBatchPaymentIndexGrain idx = GrainFactory.GetGrain<IARBatchPaymentIndexGrain>("AR-BATCH-IDX");
        await idx.AddOrUpdateAsync(new ARBatchPaymentIndexEntry
        {
            BatchId       = _state.State.BatchId,
            BatchDate     = _state.State.BatchDate,
            FacilityName  = _state.State.FacilityName,
            PaymentMethod = _state.State.PaymentMethod,
            TotalAmount   = _state.State.TotalAmount,
            IsPosted      = true,
            PaymentCount  = _state.State.Payments.Count,
        });

        await _state.WriteStateAsync();
    }
}
