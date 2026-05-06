// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single MailMan message.
/// VistA MESSAGE file (#3.9). MailMan is the internal user-to-user messaging system.
/// MUMPS references: XM.m, XMXSEND.m, XMXUTIL.m
/// </summary>
[GenerateSerializer]
public class MailMessageState
{
    /// <summary>
    /// Unique message identifier
    /// </summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Message subject line (.01)
    /// </summary>
    [Id(1)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Message body text (2)
    /// </summary>
    [Id(2)]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// User ID of the sender (1)
    /// </summary>
    [Id(3)]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the sender
    /// </summary>
    [Id(4)]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Date/time the message was sent (3)
    /// </summary>
    [Id(5)]
    public DateTime SentDateTime { get; set; }

    /// <summary>
    /// Direct (TO) recipients
    /// </summary>
    [Id(6)]
    public List<MailRecipient> Recipients { get; set; } = new();

    /// <summary>
    /// Carbon copy (CC) recipients
    /// </summary>
    [Id(7)]
    public List<MailRecipient> CcRecipients { get; set; } = new();

    /// <summary>
    /// Message ID this is a reply to, for threading
    /// </summary>
    [Id(8)]
    public string? InReplyToMessageId { get; set; }

    /// <summary>
    /// Message priority: ROUTINE, HIGH, URGENT
    /// </summary>
    [Id(9)]
    public string Priority { get; set; } = "ROUTINE";

    /// <summary>
    /// Whether the message is marked confidential
    /// </summary>
    [Id(10)]
    public bool IsConfidential { get; set; }

    /// <summary>
    /// Message status: DRAFT, SENT, DELETED
    /// </summary>
    [Id(11)]
    public string Status { get; set; } = "DRAFT";

    /// <summary>
    /// Date/time the message was last modified
    /// </summary>
    [Id(12)]
    public DateTime? LastModifiedDate { get; set; }

    /// <summary>
    /// Date/time the message was created
    /// </summary>
    [Id(13)]
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Text descriptions of attachments (not actual file data)
    /// </summary>
    [Id(14)]
    public List<string> AttachmentDescriptions { get; set; } = new();

    /// <summary>
    /// Network name for interfacility mail (VistA MailMan S.domain)
    /// </summary>
    [Id(15)]
    public string? NetworkName { get; set; }
}

/// <summary>
/// Represents a recipient of a MailMan message.
/// </summary>
[GenerateSerializer]
public class MailRecipient
{
    /// <summary>
    /// Recipient user or group identifier
    /// </summary>
    [Id(0)]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Recipient display name
    /// </summary>
    [Id(1)]
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>
    /// Recipient type: USER or MAIL_GROUP
    /// </summary>
    [Id(2)]
    public string RecipientType { get; set; } = "USER";

    /// <summary>
    /// Whether the recipient has read the message
    /// </summary>
    [Id(3)]
    public bool IsRead { get; set; }

    /// <summary>
    /// Date/time the recipient read the message
    /// </summary>
    [Id(4)]
    public DateTime? ReadDateTime { get; set; }
}
