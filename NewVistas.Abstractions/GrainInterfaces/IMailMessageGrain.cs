// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Mail Message Grain Interface based on VistA MESSAGE file (#3.9).
/// Represents a single MailMan message. Key: "MAIL-{guid}".
/// MUMPS references: XM.m, XMXSEND.m, XMXUTIL.m
/// </summary>
public interface IMailMessageGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete message state.
    /// </summary>
    Task<GrainStates.MailMessageState> GetMessageAsync();

    /// <summary>
    /// Creates a new mail message in DRAFT status.
    /// </summary>
    Task CreateMessageAsync(
        string messageId,
        string subject,
        string body,
        string senderId,
        string senderName,
        List<GrainStates.MailRecipient> recipients,
        List<GrainStates.MailRecipient>? ccRecipients,
        string priority,
        bool isConfidential,
        string? inReplyToMessageId);

    /// <summary>
    /// Sends the message — changes status from DRAFT to SENT and sets SentDateTime.
    /// </summary>
    Task SendAsync();

    /// <summary>
    /// Marks the message as read for a specific recipient.
    /// </summary>
    Task MarkReadAsync(string recipientId);

    /// <summary>
    /// Marks the message as unread for a specific recipient.
    /// </summary>
    Task MarkUnreadAsync(string recipientId);

    /// <summary>
    /// Soft-deletes the message by setting status to DELETED.
    /// </summary>
    Task DeleteAsync();

    /// <summary>
    /// Adds a text description of an attachment to the message.
    /// </summary>
    Task AddAttachmentDescriptionAsync(string description);
}
