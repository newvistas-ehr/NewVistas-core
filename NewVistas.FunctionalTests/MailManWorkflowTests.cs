// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA MailMan messaging system — Files #3.6, #3.7, #3.8, #3.9.
/// Tests end-to-end messaging workflows via grain interfaces directly.
/// </summary>
[TestFixture]
public class MailManWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IMailMessageGrain NewMessageGrain()
        => _cluster.GrainFactory.GetGrain<IMailMessageGrain>($"MAIL-{Guid.NewGuid():N}");

    private IUserMailboxGrain NewMailboxGrain(string userId)
        => _cluster.GrainFactory.GetGrain<IUserMailboxGrain>($"MAILBOX:{userId}");

    private IMailGroupGrain NewGroupGrain(string groupName)
        => _cluster.GrainFactory.GetGrain<IMailGroupGrain>($"MAILGRP:{groupName}");

    private IMailGroupIndexGrain GroupIndexGrain()
        => _cluster.GrainFactory.GetGrain<IMailGroupIndexGrain>("MAILGRP-INDEX");

    private IBulletinGrain NewBulletinGrain(string bulletinName)
        => _cluster.GrainFactory.GetGrain<IBulletinGrain>($"BULLETIN:{bulletinName}");

    // ─── MailMessage Tests ───────────────────────────────────────────────────

    [Test]
    public async Task MailMessage_CreateAndGet()
    {
        // Arrange
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        IMailMessageGrain grain = _cluster.GrainFactory.GetGrain<IMailMessageGrain>(messageId);
        List<MailRecipient> recipients =
        [
            new MailRecipient { RecipientId = "USER-001", RecipientName = "Dr. Smith", RecipientType = "USER" }
        ];

        // Act
        await grain.CreateMessageAsync(
            messageId, "Test Subject", "Test Body",
            "USER-SENDER", "Dr. Sender",
            recipients, null, "ROUTINE", false, null);
        MailMessageState state = await grain.GetMessageAsync();

        // Assert
        Assert.That(state.MessageId, Is.EqualTo(messageId));
        Assert.That(state.Subject, Is.EqualTo("Test Subject"));
        Assert.That(state.Body, Is.EqualTo("Test Body"));
        Assert.That(state.SenderId, Is.EqualTo("USER-SENDER"));
        Assert.That(state.Status, Is.EqualTo("DRAFT"));
    }

    [Test]
    public async Task MailMessage_SendSetsStatus()
    {
        // Arrange
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        IMailMessageGrain grain = _cluster.GrainFactory.GetGrain<IMailMessageGrain>(messageId);
        List<MailRecipient> recipients =
        [
            new MailRecipient { RecipientId = "USER-002", RecipientName = "Dr. Jones", RecipientType = "USER" }
        ];
        await grain.CreateMessageAsync(
            messageId, "Send Test", "Body",
            "USER-SENDER-2", "Sender Two",
            recipients, null, "HIGH", false, null);

        // Act
        await grain.SendAsync();
        MailMessageState state = await grain.GetMessageAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("SENT"));
        Assert.That(state.SentDateTime, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task MailMessage_MarkRead()
    {
        // Arrange
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        IMailMessageGrain grain = _cluster.GrainFactory.GetGrain<IMailMessageGrain>(messageId);
        List<MailRecipient> recipients =
        [
            new MailRecipient { RecipientId = "USER-003", RecipientName = "Dr. Read", RecipientType = "USER" }
        ];
        await grain.CreateMessageAsync(
            messageId, "Read Test", "Body",
            "USER-SENDER-3", "Sender Three",
            recipients, null, "ROUTINE", false, null);
        await grain.SendAsync();

        // Act
        await grain.MarkReadAsync("USER-003");
        MailMessageState state = await grain.GetMessageAsync();

        // Assert
        MailRecipient recipient = state.Recipients.First(r => r.RecipientId == "USER-003");
        Assert.That(recipient.IsRead, Is.True);
        Assert.That(recipient.ReadDateTime, Is.Not.Null);
    }

    [Test]
    public async Task MailMessage_MarkUnread()
    {
        // Arrange
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        IMailMessageGrain grain = _cluster.GrainFactory.GetGrain<IMailMessageGrain>(messageId);
        List<MailRecipient> recipients =
        [
            new MailRecipient { RecipientId = "USER-004", RecipientName = "Dr. Unread", RecipientType = "USER" }
        ];
        await grain.CreateMessageAsync(
            messageId, "Unread Test", "Body",
            "USER-SENDER-4", "Sender Four",
            recipients, null, "ROUTINE", false, null);
        await grain.SendAsync();
        await grain.MarkReadAsync("USER-004");

        // Act
        await grain.MarkUnreadAsync("USER-004");
        MailMessageState state = await grain.GetMessageAsync();

        // Assert
        MailRecipient recipient = state.Recipients.First(r => r.RecipientId == "USER-004");
        Assert.That(recipient.IsRead, Is.False);
    }

    [Test]
    public async Task MailMessage_Delete()
    {
        // Arrange
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        IMailMessageGrain grain = _cluster.GrainFactory.GetGrain<IMailMessageGrain>(messageId);
        List<MailRecipient> recipients =
        [
            new MailRecipient { RecipientId = "USER-005", RecipientName = "Dr. Delete", RecipientType = "USER" }
        ];
        await grain.CreateMessageAsync(
            messageId, "Delete Test", "Body",
            "USER-SENDER-5", "Sender Five",
            recipients, null, "ROUTINE", false, null);
        await grain.SendAsync();

        // Act
        await grain.DeleteAsync();
        MailMessageState state = await grain.GetMessageAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("DELETED"));
    }

    [Test]
    public async Task MailMessage_AddAttachment()
    {
        // Arrange
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        IMailMessageGrain grain = _cluster.GrainFactory.GetGrain<IMailMessageGrain>(messageId);
        List<MailRecipient> recipients =
        [
            new MailRecipient { RecipientId = "USER-006", RecipientName = "Dr. Attach", RecipientType = "USER" }
        ];
        await grain.CreateMessageAsync(
            messageId, "Attachment Test", "Body with attachment",
            "USER-SENDER-6", "Sender Six",
            recipients, null, "ROUTINE", false, null);

        // Act
        await grain.AddAttachmentDescriptionAsync("Lab Results PDF — CBC Panel 03/08/2026");
        MailMessageState state = await grain.GetMessageAsync();

        // Assert
        Assert.That(state.AttachmentDescriptions, Has.Count.EqualTo(1));
        Assert.That(state.AttachmentDescriptions[0], Does.Contain("Lab Results PDF"));
    }

    // ─── UserMailbox Tests ───────────────────────────────────────────────────

    [Test]
    public async Task UserMailbox_AddInboxEntry()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);
        MailboxEntry entry = new()
        {
            MessageId = $"MAIL-{Guid.NewGuid():N}",
            Subject = "Inbox Entry Test",
            SenderName = "Dr. Sender",
            SentDateTime = DateTime.UtcNow,
            IsRead = false,
            Priority = "ROUTINE",
            FolderId = "INBOX"
        };

        // Act
        await mailbox.AddInboxEntryAsync(entry);
        List<MailboxEntry> inbox = await mailbox.GetInboxAsync();

        // Assert
        Assert.That(inbox, Has.Count.EqualTo(1));
        Assert.That(inbox[0].Subject, Is.EqualTo("Inbox Entry Test"));
    }

    [Test]
    public async Task UserMailbox_AddSentEntry()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);
        MailboxEntry entry = new()
        {
            MessageId = $"MAIL-{Guid.NewGuid():N}",
            Subject = "Sent Entry Test",
            SenderName = "Me",
            SentDateTime = DateTime.UtcNow,
            IsRead = true,
            Priority = "ROUTINE",
            FolderId = "SENT"
        };

        // Act
        await mailbox.AddSentEntryAsync(entry);
        List<MailboxEntry> sent = await mailbox.GetSentItemsAsync();

        // Assert
        Assert.That(sent, Has.Count.EqualTo(1));
        Assert.That(sent[0].Subject, Is.EqualTo("Sent Entry Test"));
    }

    [Test]
    public async Task UserMailbox_MarkRead_DecrementsUnread()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        MailboxEntry entry = new()
        {
            MessageId = messageId,
            Subject = "Unread Test",
            SenderName = "Dr. Sender",
            SentDateTime = DateTime.UtcNow,
            IsRead = false,
            Priority = "HIGH",
            FolderId = "INBOX"
        };
        await mailbox.AddInboxEntryAsync(entry);
        int beforeCount = await mailbox.GetUnreadCountAsync();

        // Act
        await mailbox.MarkMessageReadAsync(messageId);
        int afterCount = await mailbox.GetUnreadCountAsync();

        // Assert
        Assert.That(beforeCount, Is.EqualTo(1));
        Assert.That(afterCount, Is.EqualTo(0));
    }

    [Test]
    public async Task UserMailbox_MoveToDeleted()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        MailboxEntry entry = new()
        {
            MessageId = messageId,
            Subject = "Delete Me",
            SenderName = "Dr. Sender",
            SentDateTime = DateTime.UtcNow,
            IsRead = true,
            Priority = "ROUTINE",
            FolderId = "INBOX"
        };
        await mailbox.AddInboxEntryAsync(entry);

        // Act
        await mailbox.MoveToDeletedAsync(messageId);
        List<MailboxEntry> inbox = await mailbox.GetInboxAsync();
        List<MailboxEntry> deleted = await mailbox.GetDeletedItemsAsync();

        // Assert
        Assert.That(inbox, Has.Count.EqualTo(0));
        Assert.That(deleted, Has.Count.EqualTo(1));
        Assert.That(deleted[0].MessageId, Is.EqualTo(messageId));
    }

    [Test]
    public async Task UserMailbox_RestoreFromDeleted()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        MailboxEntry entry = new()
        {
            MessageId = messageId,
            Subject = "Restore Me",
            SenderName = "Dr. Sender",
            SentDateTime = DateTime.UtcNow,
            IsRead = true,
            Priority = "ROUTINE",
            FolderId = "INBOX"
        };
        await mailbox.AddInboxEntryAsync(entry);
        await mailbox.MoveToDeletedAsync(messageId);

        // Act
        await mailbox.RestoreFromDeletedAsync(messageId);
        List<MailboxEntry> inbox = await mailbox.GetInboxAsync();
        List<MailboxEntry> deleted = await mailbox.GetDeletedItemsAsync();

        // Assert
        Assert.That(inbox, Has.Count.EqualTo(1));
        Assert.That(inbox[0].MessageId, Is.EqualTo(messageId));
        Assert.That(deleted, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task UserMailbox_CreateFolder()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);

        // Act
        await mailbox.CreateFolderAsync("FOLDER-CONSULTS", "Consult Messages");
        UserMailboxState state = await mailbox.GetMailboxAsync();

        // Assert
        Assert.That(state.CustomFolders, Has.Count.EqualTo(1));
        Assert.That(state.CustomFolders[0].FolderName, Is.EqualTo("Consult Messages"));
    }

    [Test]
    public async Task UserMailbox_MoveToFolder()
    {
        // Arrange
        string userId = $"USER-{Guid.NewGuid():N}";
        IUserMailboxGrain mailbox = NewMailboxGrain(userId);
        string messageId = $"MAIL-{Guid.NewGuid():N}";
        MailboxEntry entry = new()
        {
            MessageId = messageId,
            Subject = "Move to Folder Test",
            SenderName = "Dr. Sender",
            SentDateTime = DateTime.UtcNow,
            IsRead = false,
            Priority = "ROUTINE",
            FolderId = "INBOX"
        };
        await mailbox.AddInboxEntryAsync(entry);
        await mailbox.CreateFolderAsync("FOLDER-LABS", "Lab Results");

        // Act
        await mailbox.MoveToFolderAsync(messageId, "FOLDER-LABS");
        List<MailboxEntry> inbox = await mailbox.GetInboxAsync();

        // Assert — grain keeps entry in Inbox list but updates FolderId
        MailboxEntry moved = inbox.First(e => e.MessageId == messageId);
        Assert.That(moved.FolderId, Is.EqualTo("FOLDER-LABS"));
    }

    // ─── MailGroup Tests ─────────────────────────────────────────────────────

    [Test]
    public async Task MailGroup_CreateAndGetMembers()
    {
        // Arrange
        string groupName = $"GROUP-{Guid.NewGuid():N}";
        IMailGroupGrain group = NewGroupGrain(groupName);

        // Act
        await group.CreateGroupAsync(
            groupName, "Test Group Description",
            "USER-ORG-001", "Dr. Organizer", "OPEN");
        MailGroupState state = await group.GetGroupAsync();

        // Assert
        Assert.That(state.GroupName, Is.EqualTo(groupName));
        Assert.That(state.Description, Is.EqualTo("Test Group Description"));
        Assert.That(state.MembershipType, Is.EqualTo("OPEN"));
        Assert.That(state.IsActive, Is.True);
    }

    [Test]
    public async Task MailGroup_AddAndRemoveMember()
    {
        // Arrange
        string groupName = $"GROUP-{Guid.NewGuid():N}";
        IMailGroupGrain group = NewGroupGrain(groupName);
        await group.CreateGroupAsync(
            groupName, "Member Test Group",
            "USER-ORG-002", "Dr. Organizer", "OPEN");

        // Act — Add (organizer is auto-added by CreateGroupAsync, so count starts at 1)
        await group.AddMemberAsync("USER-MEM-001", "Dr. Member One", "MEMBER");
        await group.AddMemberAsync("USER-MEM-002", "Dr. Member Two", "COORDINATOR");
        List<MailGroupMember> membersAfterAdd = await group.GetMembersAsync();

        // Assert — Add (1 organizer + 2 added = 3)
        Assert.That(membersAfterAdd, Has.Count.EqualTo(3));
        Assert.That(membersAfterAdd.Any(m => m.UserId == "USER-MEM-001"), Is.True);
        Assert.That(membersAfterAdd.Any(m => m.UserId == "USER-MEM-002"), Is.True);

        // Act — Remove
        await group.RemoveMemberAsync("USER-MEM-001");
        List<MailGroupMember> membersAfterRemove = await group.GetMembersAsync();

        // Assert — Remove (organizer + USER-MEM-002 remain)
        Assert.That(membersAfterRemove, Has.Count.EqualTo(2));
        Assert.That(membersAfterRemove.Any(m => m.UserId == "USER-MEM-002"), Is.True);
    }

    // ─── Bulletin Tests ──────────────────────────────────────────────────────

    [Test]
    public async Task Bulletin_CreateAndFire()
    {
        // Arrange
        string bulletinName = $"BULLETIN-{Guid.NewGuid():N}";
        IBulletinGrain bulletin = NewBulletinGrain(bulletinName);

        // Act — Create
        await bulletin.CreateBulletinAsync(
            bulletinName,
            "Test Bulletin",
            "PHARMACY-ALERTS",
            "NEW_ORDER",
            "New order placed for {{PatientName}}");
        BulletinState stateBeforeFire = await bulletin.GetBulletinAsync();

        // Assert — Create
        Assert.That(stateBeforeFire.BulletinName, Is.EqualTo(bulletinName));
        Assert.That(stateBeforeFire.TriggerEvent, Is.EqualTo("NEW_ORDER"));
        Assert.That(stateBeforeFire.FireCount, Is.EqualTo(0));
        Assert.That(stateBeforeFire.IsActive, Is.True);

        // Act — Fire
        await bulletin.FireBulletinAsync();
        await bulletin.FireBulletinAsync();
        BulletinState stateAfterFire = await bulletin.GetBulletinAsync();

        // Assert — Fire
        Assert.That(stateAfterFire.FireCount, Is.EqualTo(2));
        Assert.That(stateAfterFire.LastFiredDateTime, Is.Not.Null);
    }
}
