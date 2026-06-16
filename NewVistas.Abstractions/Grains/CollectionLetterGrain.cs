// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CollectionLetterGrain : Grain, ICollectionLetterGrain
{
    private readonly IPersistentState<CollectionLetterState> _state;

    public CollectionLetterGrain(
        [PersistentState("collectionLetterState", "collectionLetterStore")]
        IPersistentState<CollectionLetterState> state)
    {
        _state = state;
    }

    public Task<CollectionLetterState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task<string> GenerateAsync(
        string patientId,
        string patientName,
        string? mailingAddress,
        CollectionLetterType letterType,
        List<CollectionLetterLineItem> lineItems,
        decimal totalAmountDue,
        DateTime? paymentDueDate,
        int responseDays,
        int dunningSequence,
        string? generatedByUserId,
        string? generatedByUserName,
        string? notes)
    {
        string letterId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;

        _state.State.LetterId            = letterId;
        _state.State.PatientId           = patientId;
        _state.State.PatientName         = patientName;
        _state.State.MailingAddress       = mailingAddress;
        _state.State.LetterType          = letterType;
        _state.State.Status              = CollectionLetterStatus.Generated;
        _state.State.LineItems           = lineItems;
        _state.State.TotalAmountDue      = totalAmountDue;
        _state.State.PaymentDueDate      = paymentDueDate ?? now.AddDays(responseDays);
        _state.State.ResponseDays        = responseDays;
        _state.State.DunningSequence     = dunningSequence;
        _state.State.GeneratedByUserId   = generatedByUserId;
        _state.State.GeneratedByUserName = generatedByUserName;
        _state.State.Notes               = notes;
        _state.State.GeneratedDate       = now;
        _state.State.CreatedDate         = now;
        _state.State.LastModifiedDate    = now;

        // Generate letter body based on type
        _state.State.LetterBody = GenerateLetterBody(letterType, patientName, totalAmountDue, lineItems, responseDays);

        await _state.WriteStateAsync();
        return letterId;
    }

    public async Task MarkPrintedAsync()
    {
        _state.State.Status           = CollectionLetterStatus.Printed;
        _state.State.PrintedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkMailedAsync()
    {
        _state.State.Status           = CollectionLetterStatus.Mailed;
        _state.State.MailedDate       = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkReturnedAsync()
    {
        _state.State.Status           = CollectionLetterStatus.Returned;
        _state.State.ReturnedDate     = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string? reason)
    {
        _state.State.Status           = CollectionLetterStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        if (reason is not null)
            _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes) ? reason : $"{_state.State.Notes}; {reason}";
        await _state.WriteStateAsync();
    }

    private static string GenerateLetterBody(
        CollectionLetterType letterType,
        string patientName,
        decimal totalAmountDue,
        List<CollectionLetterLineItem> lineItems,
        int responseDays)
    {
        string header = $"Dear {patientName},\n\n";
        string itemList = string.Join("\n", lineItems.Select(li =>
            $"  - {li.Description}: ${li.CurrentBalance:F2} (Original: ${li.OriginalAmount:F2}, {li.DaysPastDue} days past due)"));

        string body = letterType switch
        {
            CollectionLetterType.FirstNotice =>
                $"{header}This is a friendly reminder that you have an outstanding balance of ${totalAmountDue:F2} on your account.\n\n{itemList}\n\nPlease remit payment within {responseDays} days. If you have questions, please contact our billing office.",
            CollectionLetterType.SecondNotice =>
                $"{header}SECOND NOTICE: Your account remains past due with a balance of ${totalAmountDue:F2}.\n\n{itemList}\n\nInterest and/or administrative charges may be added if payment is not received within {responseDays} days.",
            CollectionLetterType.ThirdNotice =>
                $"{header}FINAL NOTICE: Despite previous correspondence, your account balance of ${totalAmountDue:F2} remains unpaid.\n\n{itemList}\n\nIf payment is not received within {responseDays} days, your account may be referred to a collection agency.",
            CollectionLetterType.IntentToCollect =>
                $"{header}INTENT TO COLLECT: This letter serves as formal notification that your account balance of ${totalAmountDue:F2} will be referred to a collection agency if payment is not received within {responseDays} days.\n\n{itemList}\n\nThis referral may affect your credit. Please contact our billing office immediately to arrange payment.",
            CollectionLetterType.Statement =>
                $"{header}ACCOUNT STATEMENT\n\nYour current account balance is ${totalAmountDue:F2}.\n\n{itemList}\n\nPlease remit payment at your earliest convenience.",
            CollectionLetterType.TopPreReferral =>
                $"{header}IMPORTANT: TREASURY OFFSET PROGRAM NOTICE\n\nYour account balance of ${totalAmountDue:F2} is scheduled for referral to the U.S. Treasury Offset Program.\n\n{itemList}\n\nFederal payments (tax refunds, Social Security) may be offset to satisfy this debt. Contact our office within {responseDays} days to avoid referral.",
            _ =>
                $"{header}Your account has a balance of ${totalAmountDue:F2}.\n\n{itemList}\n\nPlease contact our billing office for details.",
        };

        return body;
    }
}
