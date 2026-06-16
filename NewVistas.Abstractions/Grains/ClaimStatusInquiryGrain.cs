// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ClaimStatusInquiryGrain : Grain, IClaimStatusInquiryGrain
{
    private readonly IPersistentState<ClaimStatusInquiryState> _state;

    public ClaimStatusInquiryGrain(
        [PersistentState("claimStatusInquiryState", "claimStatusInquiryStore")]
        IPersistentState<ClaimStatusInquiryState> state)
    {
        _state = state;
    }

    public Task<ClaimStatusInquiryState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId, string claimId, string payerId, string payerName,
        decimal? totalBilledAmount, string? initiatedByUserId, string? initiatedByUserName, string? notes)
    {
        string inquiryId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;
        _state.State.InquiryId           = inquiryId;
        _state.State.PatientId           = patientId;
        _state.State.ClaimId             = claimId;
        _state.State.PayerId             = payerId;
        _state.State.PayerName           = payerName;
        _state.State.TotalBilledAmount   = totalBilledAmount;
        _state.State.InitiatedByUserId   = initiatedByUserId;
        _state.State.InitiatedByUserName = initiatedByUserName;
        _state.State.Notes               = notes;
        _state.State.Status              = ClaimStatusInquiryStatus.Draft;
        _state.State.CreatedDate         = now;
        _state.State.LastModifiedDate    = now;
        await _state.WriteStateAsync();
        return inquiryId;
    }

    public async Task SubmitAsync(string? traceNumber)
    {
        _state.State.Status           = ClaimStatusInquiryStatus.Submitted;
        _state.State.SubmittedDate    = DateTime.UtcNow;
        _state.State.TraceNumber      = traceNumber;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordResponseAsync(
        ClaimStatusCategory claimStatusCategory,
        List<ClaimStatusDetail> statusDetails,
        decimal? reportedPaidAmount,
        string? payerMessage)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.Status              = ClaimStatusInquiryStatus.ResponseReceived;
        _state.State.ResponseDate        = now;
        _state.State.ClaimStatusCategory = claimStatusCategory;
        _state.State.StatusDetails       = statusDetails;
        _state.State.ReportedPaidAmount  = reportedPaidAmount;
        _state.State.PayerMessage        = payerMessage;
        _state.State.LastModifiedDate    = now;
        await _state.WriteStateAsync();
    }

    public async Task RecordErrorAsync(List<string> rejectReasonCodes, string? payerMessage)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.Status            = ClaimStatusInquiryStatus.Error;
        _state.State.ResponseDate      = now;
        _state.State.RejectReasonCodes = rejectReasonCodes;
        _state.State.PayerMessage      = payerMessage;
        _state.State.LastModifiedDate  = now;
        await _state.WriteStateAsync();
    }
}
