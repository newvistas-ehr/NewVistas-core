// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Direct Message Grain — manages a single Direct message (C-CDA exchange).
/// §170.315(h)(1) — Applicability Statement for Secure Health Transport (§170.202(a)(2)).
/// </summary>
public class DirectMessageGrain : Grain, IDirectMessageGrain
{
    private readonly IPersistentState<DirectMessageState> _state;

    public DirectMessageGrain(
        [PersistentState("directMessageState", "directMessageStore")] IPersistentState<DirectMessageState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.MessageId))
            _state.State.MessageId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateMessageAsync(
        string direction,
        string fromAddress,
        string toAddress,
        string subject,
        string patientId,
        string? patientName,
        string documentType,
        string ccdaContent,
        string? sentBy)
    {
        _state.State.Direction = direction;
        _state.State.FromAddress = fromAddress;
        _state.State.ToAddress = toAddress;
        _state.State.Subject = subject;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.DocumentType = documentType;
        _state.State.CcdaContent = ccdaContent;
        _state.State.SentBy = sentBy;
        _state.State.Status = direction == "inbound" ? "received" : "draft";
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkSendingAsync()
    {
        _state.State.Status = "sending";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkSentAsync(bool isEncrypted, bool isSigned, string? signingCertThumbprint, string? encryptionCertThumbprint)
    {
        _state.State.Status = "sent";
        _state.State.SentDate = DateTime.UtcNow;
        _state.State.IsEncrypted = isEncrypted;
        _state.State.IsSigned = isSigned;
        _state.State.SigningCertThumbprint = signingCertThumbprint;
        _state.State.EncryptionCertThumbprint = encryptionCertThumbprint;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeliveredAsync()
    {
        _state.State.Status = "delivered";
        _state.State.DeliveredDate = DateTime.UtcNow;
        _state.State.MdnStatus = "processed";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkFailedAsync(string reason)
    {
        _state.State.Status = "failed";
        _state.State.FailureReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordMdnAsync(string mdnStatus, string? mdnContent)
    {
        _state.State.MdnStatus = mdnStatus;
        _state.State.MdnContent = mdnContent;
        _state.State.MdnDate = DateTime.UtcNow;

        if (mdnStatus == "processed" || mdnStatus == "displayed")
        {
            _state.State.Status = "delivered";
            _state.State.DeliveredDate = DateTime.UtcNow;
        }
        else if (mdnStatus == "failed" || mdnStatus == "denied")
        {
            _state.State.Status = "failed";
            _state.State.FailureReason = $"MDN: {mdnStatus}";
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<DirectMessageState> GetMessageAsync() => Task.FromResult(_state.State);
}

/// <summary>
/// Direct Message Index Grain — listing of all Direct messages.
/// </summary>
public class DirectMessageIndexGrain : Grain, IDirectMessageIndexGrain
{
    private readonly IPersistentState<DirectMessageIndexState> _state;

    public DirectMessageIndexGrain(
        [PersistentState("directMessageIndexState", "directMessageIndexStore")] IPersistentState<DirectMessageIndexState> state)
    {
        _state = state;
    }

    public async Task AddMessageAsync(DirectMessageSummary summary)
    {
        _state.State.Messages.RemoveAll(m => m.MessageId == summary.MessageId);
        _state.State.Messages.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string messageId, string status)
    {
        DirectMessageSummary? existing = _state.State.Messages.FirstOrDefault(m => m.MessageId == messageId);
        if (existing != null)
        {
            existing.Status = status;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateMdnStatusAsync(string messageId, string mdnStatus)
    {
        DirectMessageSummary? existing = _state.State.Messages.FirstOrDefault(m => m.MessageId == messageId);
        if (existing != null)
        {
            existing.MdnStatus = mdnStatus;
            if (mdnStatus == "processed" || mdnStatus == "displayed")
                existing.Status = "delivered";
            else if (mdnStatus == "failed" || mdnStatus == "denied")
                existing.Status = "failed";
            await _state.WriteStateAsync();
        }
    }

    public Task<List<DirectMessageSummary>> GetAllMessagesAsync()
        => Task.FromResult(_state.State.Messages.OrderByDescending(m => m.CreatedDate).ToList());

    public Task<List<DirectMessageSummary>> GetOutboundMessagesAsync()
        => Task.FromResult(_state.State.Messages
            .Where(m => m.Direction == "outbound")
            .OrderByDescending(m => m.CreatedDate).ToList());

    public Task<List<DirectMessageSummary>> GetInboundMessagesAsync()
        => Task.FromResult(_state.State.Messages
            .Where(m => m.Direction == "inbound")
            .OrderByDescending(m => m.CreatedDate).ToList());

    public Task<List<DirectMessageSummary>> GetMessagesByPatientAsync(string patientId, int maxResults = 50)
        => Task.FromResult(_state.State.Messages
            .Where(m => m.PatientId == patientId)
            .OrderByDescending(m => m.CreatedDate).Take(maxResults).ToList());

    public Task<List<DirectMessageSummary>> GetMessagesByStatusAsync(string status)
        => Task.FromResult(_state.State.Messages
            .Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.CreatedDate).ToList());

    public Task<List<DirectMessageSummary>> GetPendingDeliveryAsync()
        => Task.FromResult(_state.State.Messages
            .Where(m => m.Direction == "outbound" && m.Status == "sent" && m.MdnStatus == "none")
            .OrderByDescending(m => m.CreatedDate).ToList());
}
