// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class UserMailboxGrain : Grain, IUserMailboxGrain
{
    private readonly IPersistentState<UserMailboxState> _state;

    public UserMailboxGrain(
        [PersistentState("userMailboxState", "userMailboxStore")]
        IPersistentState<UserMailboxState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.UserId))
        {
            _state.State.UserId = this.GetPrimaryKeyString();
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<UserMailboxState> GetMailboxAsync() => Task.FromResult(_state.State);

    public Task<List<MailboxEntry>> GetInboxAsync() => Task.FromResult(_state.State.Inbox);

    public Task<List<MailboxEntry>> GetSentItemsAsync() => Task.FromResult(_state.State.SentItems);

    public Task<List<MailboxEntry>> GetDeletedItemsAsync() => Task.FromResult(_state.State.DeletedItems);

    public async Task AddInboxEntryAsync(MailboxEntry entry)
    {
        entry.FolderId = "INBOX";
        _state.State.Inbox.Add(entry);
        if (!entry.IsRead)
        {
            _state.State.UnreadCount++;
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSentEntryAsync(MailboxEntry entry)
    {
        entry.FolderId = "SENT";
        _state.State.SentItems.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkMessageReadAsync(string messageId)
    {
        MailboxEntry? entry = _state.State.Inbox.FirstOrDefault(e => e.MessageId == messageId);
        if (entry != null && !entry.IsRead)
        {
            entry.IsRead = true;
            _state.State.UnreadCount = Math.Max(0, _state.State.UnreadCount - 1);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task MarkMessageUnreadAsync(string messageId)
    {
        MailboxEntry? entry = _state.State.Inbox.FirstOrDefault(e => e.MessageId == messageId);
        if (entry != null && entry.IsRead)
        {
            entry.IsRead = false;
            _state.State.UnreadCount++;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task MoveToDeletedAsync(string messageId)
    {
        // Check inbox first
        MailboxEntry? entry = _state.State.Inbox.FirstOrDefault(e => e.MessageId == messageId);
        if (entry != null)
        {
            _state.State.Inbox.Remove(entry);
            if (!entry.IsRead)
            {
                _state.State.UnreadCount = Math.Max(0, _state.State.UnreadCount - 1);
            }
            entry.FolderId = "DELETED";
            _state.State.DeletedItems.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RestoreFromDeletedAsync(string messageId)
    {
        MailboxEntry? entry = _state.State.DeletedItems.FirstOrDefault(e => e.MessageId == messageId);
        if (entry != null)
        {
            _state.State.DeletedItems.Remove(entry);
            entry.FolderId = "INBOX";
            _state.State.Inbox.Add(entry);
            if (!entry.IsRead)
            {
                _state.State.UnreadCount++;
            }
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task PurgeDeletedAsync()
    {
        _state.State.DeletedItems.Clear();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CreateFolderAsync(string folderId, string folderName)
    {
        if (!_state.State.CustomFolders.Any(f => f.FolderId == folderId))
        {
            _state.State.CustomFolders.Add(new MailboxFolder
            {
                FolderId = folderId,
                FolderName = folderName,
                CreatedDate = DateTime.UtcNow
            });
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task MoveToFolderAsync(string messageId, string folderId)
    {
        MailboxEntry? entry = _state.State.Inbox.FirstOrDefault(e => e.MessageId == messageId);
        if (entry != null)
        {
            entry.FolderId = folderId;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<int> GetUnreadCountAsync() => Task.FromResult(_state.State.UnreadCount);
}
