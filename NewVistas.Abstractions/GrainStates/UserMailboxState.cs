// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a user's mailbox with inbox, sent items, deleted items, and custom folders.
/// VistA MAILBOX file (#3.7). MUMPS references: XMXUTIL.m, XMJM.m
/// </summary>
[GenerateSerializer]
public class UserMailboxState
{
    /// <summary>
    /// User identifier who owns this mailbox
    /// </summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Received messages
    /// </summary>
    [Id(1)]
    public List<MailboxEntry> Inbox { get; set; } = new();

    /// <summary>
    /// Messages sent by this user
    /// </summary>
    [Id(2)]
    public List<MailboxEntry> SentItems { get; set; } = new();

    /// <summary>
    /// Soft-deleted messages
    /// </summary>
    [Id(3)]
    public List<MailboxEntry> DeletedItems { get; set; } = new();

    /// <summary>
    /// User-created mail folders
    /// </summary>
    [Id(4)]
    public List<MailboxFolder> CustomFolders { get; set; } = new();

    /// <summary>
    /// Count of unread messages in inbox
    /// </summary>
    [Id(5)]
    public int UnreadCount { get; set; }

    /// <summary>
    /// Date/time the mailbox was last modified
    /// </summary>
    [Id(6)]
    public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight projection of a message for mailbox listing.
/// </summary>
[GenerateSerializer]
public class MailboxEntry
{
    /// <summary>
    /// Message identifier
    /// </summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Message subject
    /// </summary>
    [Id(1)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Sender display name
    /// </summary>
    [Id(2)]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Date/time the message was sent
    /// </summary>
    [Id(3)]
    public DateTime SentDateTime { get; set; }

    /// <summary>
    /// Whether the message has been read
    /// </summary>
    [Id(4)]
    public bool IsRead { get; set; }

    /// <summary>
    /// Message priority: ROUTINE, HIGH, URGENT
    /// </summary>
    [Id(5)]
    public string Priority { get; set; } = "ROUTINE";

    /// <summary>
    /// Folder this entry belongs to: INBOX, SENT, DELETED, or custom folder ID
    /// </summary>
    [Id(6)]
    public string FolderId { get; set; } = "INBOX";

    /// <summary>
    /// Whether the message is marked confidential
    /// </summary>
    [Id(7)]
    public bool IsConfidential { get; set; }
}

/// <summary>
/// Represents a user-created mail folder.
/// </summary>
[GenerateSerializer]
public class MailboxFolder
{
    /// <summary>
    /// Unique folder identifier
    /// </summary>
    [Id(0)]
    public string FolderId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the folder
    /// </summary>
    [Id(1)]
    public string FolderName { get; set; } = string.Empty;

    /// <summary>
    /// Date/time the folder was created
    /// </summary>
    [Id(2)]
    public DateTime CreatedDate { get; set; }
}
