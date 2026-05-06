// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MailMessageGrain : Grain, IMailMessageGrain
{
    private readonly IPersistentState<MailMessageState> _state;

    public MailMessageGrain(
        [PersistentState("mailMessageState", "mailMessageStore")]
        IPersistentState<MailMessageState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.MessageId))
        {
            _state.State.MessageId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<MailMessageState> GetMessageAsync() => Task.FromResult(_state.State);

    public async Task CreateMessageAsync(
        string messageId,
        string subject,
        string body,
        string senderId,
        string senderName,
        List<MailRecipient> recipients,
        List<MailRecipient>? ccRecipients,
        string priority,
        bool isConfidential,
        string? inReplyToMessageId)
    {
        _state.State.MessageId = messageId;
        _state.State.Subject = subject;
        _state.State.Body = body;
        _state.State.SenderId = senderId;
        _state.State.SenderName = senderName;
        _state.State.Recipients = recipients;
        _state.State.CcRecipients = ccRecipients ?? new List<MailRecipient>();
        _state.State.Priority = priority;
        _state.State.IsConfidential = isConfidential;
        _state.State.InReplyToMessageId = inReplyToMessageId;
        _state.State.Status = "DRAFT";
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SendAsync()
    {
        _state.State.Status = "SENT";
        _state.State.SentDateTime = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkReadAsync(string recipientId)
    {
        MailRecipient? recipient = _state.State.Recipients.FirstOrDefault(r => r.RecipientId == recipientId)
            ?? _state.State.CcRecipients.FirstOrDefault(r => r.RecipientId == recipientId);

        if (recipient != null && !recipient.IsRead)
        {
            recipient.IsRead = true;
            recipient.ReadDateTime = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task MarkUnreadAsync(string recipientId)
    {
        MailRecipient? recipient = _state.State.Recipients.FirstOrDefault(r => r.RecipientId == recipientId)
            ?? _state.State.CcRecipients.FirstOrDefault(r => r.RecipientId == recipientId);

        if (recipient != null && recipient.IsRead)
        {
            recipient.IsRead = false;
            recipient.ReadDateTime = null;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task DeleteAsync()
    {
        _state.State.Status = "DELETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAttachmentDescriptionAsync(string description)
    {
        if (!_state.State.AttachmentDescriptions.Contains(description))
        {
            _state.State.AttachmentDescriptions.Add(description);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }
}
