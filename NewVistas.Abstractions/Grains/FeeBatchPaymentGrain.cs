// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeBatchPaymentGrain : Grain, IFeeBatchPaymentGrain
{
    private readonly IPersistentState<FeeBatchPaymentState> _state;

    public FeeBatchPaymentGrain(
        [PersistentState("feeBatchPaymentState", "feeBatchPaymentStore")]
        IPersistentState<FeeBatchPaymentState> state)
    {
        _state = state;
    }

    public Task<FeeBatchPaymentState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string? vendorId,
        string? vendorName,
        DateTime batchDate,
        string paymentMethod,
        string? checkNumber,
        string? notes)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.BatchId       = this.GetPrimaryKeyString();
        _state.State.VendorId      = vendorId;
        _state.State.VendorName    = vendorName;
        _state.State.BatchDate     = batchDate;
        _state.State.PaymentMethod = paymentMethod;
        _state.State.CheckNumber   = checkNumber;
        _state.State.TotalAmount   = 0m;
        _state.State.IsPosted      = false;
        _state.State.Notes         = notes;
        _state.State.CreatedDate   = now;
        _state.State.LastModifiedDate = now;

        await _state.WriteStateAsync();
    }

    public async Task AddInvoiceAsync(
        string invoiceId,
        string authorizationId,
        string patientId,
        string patientName,
        string vendorId,
        string vendorName,
        decimal paidAmount)
    {
        // Prevent duplicate entries
        if (_state.State.InvoiceEntries.Any(e => e.InvoiceId == invoiceId))
            return;

        _state.State.InvoiceEntries.Add(new FeeBatchPaymentEntry
        {
            InvoiceId       = invoiceId,
            AuthorizationId = authorizationId,
            PatientId       = patientId,
            PatientName     = patientName,
            VendorId        = vendorId,
            VendorName      = vendorName,
            PaidAmount      = paidAmount,
        });

        _state.State.TotalAmount      += paidAmount;
        _state.State.LastModifiedDate  = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task PostAsync(string postedByUserId, string postedByUserName)
    {
        // Call PayAsync on each invoice grain.
        // IFeeInvoiceGrain.PayAsync internally calls IFeeAuthorizationGrain.RecordInvoicePaymentAsync.
        foreach (FeeBatchPaymentEntry entry in _state.State.InvoiceEntries)
        {
            IFeeInvoiceGrain invoice = GrainFactory.GetGrain<IFeeInvoiceGrain>(entry.InvoiceId);
            await invoice.PayAsync(
                entry.PaidAmount,
                _state.State.PaymentMethod,
                _state.State.CheckNumber,
                _state.State.BatchDate);
        }

        _state.State.IsPosted         = true;
        _state.State.PostedByUserId   = postedByUserId;
        _state.State.PostedByUserName = postedByUserName;
        _state.State.PostedDate       = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }
}
