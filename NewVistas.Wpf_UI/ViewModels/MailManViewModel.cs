// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class MailManViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _userId = string.Empty;

    // Messages
    [ObservableProperty] private string _activeFolder = "inbox";
    [ObservableProperty] private ObservableCollection<MailboxEntry> _messages = new();
    [ObservableProperty] private MailboxEntry? _selectedMessage;
    [ObservableProperty] private int _unreadCount;

    // Compose
    [ObservableProperty] private string _composeTo = string.Empty;
    [ObservableProperty] private string _composeCc = string.Empty;
    [ObservableProperty] private string _composeSubject = string.Empty;
    [ObservableProperty] private string _composeBody = string.Empty;
    [ObservableProperty] private string _composePriority = "ROUTINE";
    [ObservableProperty] private bool _composeConfidential;

    // Groups
    // The index projection, not the full group state: it carries Name/Description/MemberCount/
    // IsActive, which is exactly what the grid shows. (The grid previously bound Name and
    // Status against MailGroupState, which has neither, so those columns were always blank.)
    [ObservableProperty] private ObservableCollection<MailGroupIndexEntry> _groups = new();
    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newGroupDescription = string.Empty;
    [ObservableProperty] private string _newGroupType = "PUBLIC";

    public string[] PriorityOptions { get; } = ["ROUTINE", "HIGH", "LOW"];
    public string[] GroupTypes { get; } = ["PUBLIC", "PRIVATE"];

    public MailManViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    /// <summary>
    /// Splits a comma/semicolon-separated recipient box into recipients. An entry prefixed
    /// with <c>g:</c> is treated as a mail group so group delivery can be exercised.
    /// </summary>
    private static List<MailRecipient> ParseRecipients(string raw) =>
        (raw ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.StartsWith("g:", StringComparison.OrdinalIgnoreCase)
                ? new MailRecipient { RecipientId = entry[2..], RecipientName = entry[2..], RecipientType = "MAIL_GROUP" }
                : new MailRecipient { RecipientId = entry, RecipientName = entry, RecipientType = "USER" })
            .ToList();

    // Grain keys match the ones the MailMan controller uses, so both surfaces address the
    // same grains rather than two parallel worlds.
    private IMailGroupIndexGrain GroupIndex() => _grains.GetGrain<IMailGroupIndexGrain>("MAILGRP-INDEX");
    private IMailGroupGrain Group(string name) => _grains.GetGrain<IMailGroupGrain>($"MAILGRP:{name}");
    private IUserMailboxGrain Mailbox(string userId) => _grains.GetGrain<IUserMailboxGrain>($"MAILBOX:{userId}");
    private IMailMessageGrain Message(string messageId) => _grains.GetGrain<IMailMessageGrain>(messageId);

    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) { Error = "Enter a User ID"; return; }
        IsLoading = true;
        Error = null;
        try
        {
            var mailbox = _grains.GetGrain<IUserMailboxGrain>($"MAILBOX:{UserId.Trim()}");
            List<MailboxEntry> list = ActiveFolder switch
            {
                "sent" => await mailbox.GetSentItemsAsync(),
                "deleted" => await mailbox.GetDeletedItemsAsync(),
                _ => await mailbox.GetInboxAsync()
            };
            Messages.Clear();
            foreach (MailboxEntry m in list) Messages.Add(m);
            UnreadCount = await mailbox.GetUnreadCountAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void SwitchFolder(string folder)
    {
        ActiveFolder = folder;
        _ = LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        try
        {
            // Multi-grain orchestration done here rather than over HTTP: create the message,
            // send it, file it in the sender's Sent items, then deliver to each recipient's
            // inbox (expanding mail groups to their members), mirroring MailManController.
            string messageId = $"MAIL-{Guid.NewGuid():N}";

            List<MailRecipient> to = ParseRecipients(ComposeTo);
            List<MailRecipient> cc = ParseRecipients(ComposeCc);

            IMailMessageGrain msg = Message(messageId);
            await msg.CreateMessageAsync(
                messageId, ComposeSubject, ComposeBody,
                UserId, _grains.CurrentUserName ?? UserId,
                to, cc, ComposePriority, ComposeConfidential, null);
            await msg.SendAsync();

            var sent = new MailboxEntry
            {
                MessageId = messageId,
                Subject = ComposeSubject,
                SenderName = _grains.CurrentUserName ?? UserId,
                SentDateTime = DateTime.UtcNow,
                IsRead = true,
                Priority = ComposePriority,
                FolderId = "SENT",
                IsConfidential = ComposeConfidential,
            };
            await Mailbox(UserId).AddSentEntryAsync(sent);

            var delivered = new MailboxEntry
            {
                MessageId = messageId,
                Subject = ComposeSubject,
                SenderName = _grains.CurrentUserName ?? UserId,
                SentDateTime = DateTime.UtcNow,
                IsRead = false,
                Priority = ComposePriority,
                FolderId = "INBOX",
                IsConfidential = ComposeConfidential,
            };

            foreach (MailRecipient r in to.Concat(cc))
            {
                if (r.RecipientType == "MAIL_GROUP")
                {
                    MailGroupState group = await Group(r.RecipientId).GetGroupAsync();
                    foreach (MailGroupMember member in group.Members)
                        await Mailbox(member.UserId).AddInboxEntryAsync(delivered);
                }
                else
                {
                    await Mailbox(r.RecipientId).AddInboxEntryAsync(delivered);
                }
            }

            ComposeSubject = string.Empty;
            ComposeBody = string.Empty;
            ComposeTo = string.Empty;
            ComposeCc = string.Empty;
            await LoadMessagesAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteMessageAsync()
    {
        if (SelectedMessage == null) return;
        try
        {
            var mailbox = _grains.GetGrain<IUserMailboxGrain>($"MAILBOX:{UserId.Trim()}");
            await mailbox.MoveToDeletedAsync(SelectedMessage.MessageId);
            await LoadMessagesAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ToggleReadAsync()
    {
        if (SelectedMessage == null) return;
        try
        {
            var mailbox = _grains.GetGrain<IUserMailboxGrain>($"MAILBOX:{UserId.Trim()}");
            if (SelectedMessage.IsRead)
                await mailbox.MarkMessageUnreadAsync(SelectedMessage.MessageId);
            else
                await mailbox.MarkMessageReadAsync(SelectedMessage.MessageId);
            await LoadMessagesAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadGroupsAsync()
    {
        try
        {
            List<MailGroupIndexEntry> list = await GroupIndex().GetAllGroupsAsync();
            Groups.Clear();
            foreach (MailGroupIndexEntry g in list) Groups.Add(g);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        try
        {
            await Group(NewGroupName).CreateGroupAsync(
                NewGroupName, NewGroupDescription,
                _grains.CurrentUserId ?? UserId, _grains.CurrentUserName ?? UserId,
                NewGroupType);

            await GroupIndex().AddOrUpdateGroupAsync(new MailGroupIndexEntry
            {
                Name = NewGroupName,
                Description = NewGroupDescription,
                MemberCount = 0,
                IsActive = true,
            });

            NewGroupName = string.Empty;
            NewGroupDescription = string.Empty;
            await LoadGroupsAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) { Error = "Enter a User ID"; return; }

            // Compact grain-direct demo seed, matching the Blazor MailMan page. Demo seeding
            // is a developer affordance, but it still goes through grains — the UI has no
            // business calling the WebServer to populate its own screens.
            string uid = UserId.Trim();
            string msgId = $"MAIL-{Guid.NewGuid():N}";

            IMailMessageGrain msg = Message(msgId);
            await msg.CreateMessageAsync(
                msgId, "Welcome to MailMan", "This is a demo message.",
                "SYSTEM", "System",
                new List<MailRecipient> { new() { RecipientId = uid, RecipientName = uid, RecipientType = "USER" } },
                null, "ROUTINE", false, null);
            await msg.SendAsync();

            await Mailbox(uid).AddInboxEntryAsync(new MailboxEntry
            {
                MessageId = msgId,
                Subject = "Welcome to MailMan",
                SenderName = "System",
                SentDateTime = DateTime.UtcNow,
                IsRead = false,
                Priority = "ROUTINE",
                FolderId = "INBOX",
            });

            string groupName = "PHARMACY-ALERTS";
            await Group(groupName).CreateGroupAsync(
                groupName, "Pharmacy alert notifications", uid, "Demo User", "OPEN");
            await Group(groupName).AddMemberAsync(uid, "Demo User", "ORGANIZER");
            await GroupIndex().AddOrUpdateGroupAsync(new MailGroupIndexEntry
            {
                Name = groupName,
                Description = "Pharmacy alert notifications",
                MemberCount = 1,
                IsActive = true,
            });

            await LoadMessagesAsync();
            await LoadGroupsAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
