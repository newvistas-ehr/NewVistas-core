// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// User Mailbox Grain Interface based on VistA MAILBOX file (#3.7).
/// Represents a user's complete mailbox with inbox, sent, deleted, and custom folders.
/// Key: "MAILBOX:{userId}". MUMPS references: XMXUTIL.m, XMJM.m
/// </summary>
public interface IUserMailboxGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete mailbox state.
    /// </summary>
    Task<GrainStates.UserMailboxState> GetMailboxAsync();

    /// <summary>
    /// Gets all inbox entries.
    /// </summary>
    Task<List<GrainStates.MailboxEntry>> GetInboxAsync();

    /// <summary>
    /// Gets all sent item entries.
    /// </summary>
    Task<List<GrainStates.MailboxEntry>> GetSentItemsAsync();

    /// <summary>
    /// Gets all deleted item entries.
    /// </summary>
    Task<List<GrainStates.MailboxEntry>> GetDeletedItemsAsync();

    /// <summary>
    /// Adds a message entry to the inbox (called when a message is sent TO this user).
    /// </summary>
    Task AddInboxEntryAsync(GrainStates.MailboxEntry entry);

    /// <summary>
    /// Adds a message entry to sent items (called when this user sends a message).
    /// </summary>
    Task AddSentEntryAsync(GrainStates.MailboxEntry entry);

    /// <summary>
    /// Marks a message as read and decrements the unread count.
    /// </summary>
    Task MarkMessageReadAsync(string messageId);

    /// <summary>
    /// Marks a message as unread and increments the unread count.
    /// </summary>
    Task MarkMessageUnreadAsync(string messageId);

    /// <summary>
    /// Moves a message from inbox or custom folder to deleted items.
    /// </summary>
    Task MoveToDeletedAsync(string messageId);

    /// <summary>
    /// Restores a message from deleted items back to inbox.
    /// </summary>
    Task RestoreFromDeletedAsync(string messageId);

    /// <summary>
    /// Permanently removes all deleted items.
    /// </summary>
    Task PurgeDeletedAsync();

    /// <summary>
    /// Creates a new custom mail folder.
    /// </summary>
    Task CreateFolderAsync(string folderId, string folderName);

    /// <summary>
    /// Moves a message to a custom folder.
    /// </summary>
    Task MoveToFolderAsync(string messageId, string folderId);

    /// <summary>
    /// Gets the current count of unread messages.
    /// </summary>
    Task<int> GetUnreadCountAsync();
}
