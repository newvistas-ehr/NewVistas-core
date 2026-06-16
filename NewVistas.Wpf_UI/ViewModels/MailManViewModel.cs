// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class MailManViewModel : ObservableObject
{
    private readonly ApiClient _api;
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
    [ObservableProperty] private ObservableCollection<MailGroupState> _groups = new();
    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newGroupDescription = string.Empty;
    [ObservableProperty] private string _newGroupType = "PUBLIC";

    public string[] PriorityOptions { get; } = ["ROUTINE", "HIGH", "LOW"];
    public string[] GroupTypes { get; } = ["PUBLIC", "PRIVATE"];

    public MailManViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

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
            // Message creation still goes through API as it involves multiple grains
            await _api.Http.PostAsJsonAsync("api/mailman/messages", new
            {
                From = UserId,
                To = ComposeTo,
                Cc = ComposeCc,
                Subject = ComposeSubject,
                Body = ComposeBody,
                Priority = ComposePriority,
                Confidential = ComposeConfidential
            });
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
            // Groups listing goes through API since there's no singleton group index grain
            var list = await _api.Http.GetFromJsonAsync<List<MailGroupState>>(
                "api/mailman/groups", ApiClient.Json) ?? [];
            Groups.Clear();
            foreach (MailGroupState g in list) Groups.Add(g);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        try
        {
            await _api.Http.PostAsJsonAsync("api/mailman/groups", new
            {
                Name = NewGroupName,
                Description = NewGroupDescription,
                MembershipType = NewGroupType
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
            await _api.Http.PostAsJsonAsync("api/mailman/demo/load", new { });
            await LoadMessagesAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
