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
/// Secure Message Thread Grain — manages a bidirectional message thread.
/// §170.315(e)(2) — Secure Messaging.
/// </summary>
public class SecureMessageThreadGrain : Grain, ISecureMessageThreadGrain
{
    private readonly IPersistentState<SecureMessageThreadState> _state;

    public SecureMessageThreadGrain(
        [PersistentState("secureMessageThreadState", "secureMessageThreadStore")] IPersistentState<SecureMessageThreadState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ThreadId))
            _state.State.ThreadId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateThreadAsync(
        string patientId,
        string? patientName,
        string subject,
        string category,
        string? assignedProviderId,
        string? assignedProviderName)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.Subject = subject;
        _state.State.Category = category;
        _state.State.AssignedProviderId = assignedProviderId;
        _state.State.AssignedProviderName = assignedProviderName;
        _state.State.Status = "open";
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastMessageDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AddMessageAsync(
        string senderType,
        string? senderId,
        string? senderName,
        string body)
    {
        var message = new SecureMessage
        {
            MessageId = $"MSG-{Guid.NewGuid():N}",
            SenderType = senderType,
            SenderId = senderId,
            SenderName = senderName,
            Body = body,
            SentDate = DateTime.UtcNow,
            IsRead = false
        };

        _state.State.Messages.Add(message);
        _state.State.LastMessageDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Mark unread for the other party
        if (senderType == "patient")
            _state.State.HasUnreadProvider = true;
        else
            _state.State.HasUnreadPatient = true;

        await _state.WriteStateAsync();
    }

    public async Task MarkReadAsync(string readerType)
    {
        if (readerType == "patient")
        {
            _state.State.HasUnreadPatient = false;
            foreach (SecureMessage msg in _state.State.Messages.Where(m => m.SenderType == "provider"))
                msg.IsRead = true;
        }
        else
        {
            _state.State.HasUnreadProvider = false;
            foreach (SecureMessage msg in _state.State.Messages.Where(m => m.SenderType == "patient"))
                msg.IsRead = true;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseThreadAsync()
    {
        _state.State.Status = "closed";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReopenThreadAsync()
    {
        _state.State.Status = "open";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<SecureMessageThreadState> GetThreadAsync() => Task.FromResult(_state.State);
}

/// <summary>
/// Per-patient secure message thread index.
/// </summary>
public class SecureMessageIndexGrain : Grain, ISecureMessageIndexGrain
{
    private readonly IPersistentState<SecureMessageIndexState> _state;

    public SecureMessageIndexGrain(
        [PersistentState("secureMessageIndexState", "secureMessageIndexStore")] IPersistentState<SecureMessageIndexState> state)
    {
        _state = state;
    }

    public async Task AddThreadAsync(SecureMessageThreadSummary summary)
    {
        _state.State.Threads.RemoveAll(t => t.ThreadId == summary.ThreadId);
        _state.State.Threads.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateThreadAsync(SecureMessageThreadSummary summary)
    {
        _state.State.Threads.RemoveAll(t => t.ThreadId == summary.ThreadId);
        _state.State.Threads.Add(summary);
        await _state.WriteStateAsync();
    }

    public Task<List<SecureMessageThreadSummary>> GetAllThreadsAsync()
        => Task.FromResult(_state.State.Threads.OrderByDescending(t => t.LastMessageDate).ToList());

    public Task<List<SecureMessageThreadSummary>> GetOpenThreadsAsync()
        => Task.FromResult(_state.State.Threads
            .Where(t => t.Status == "open")
            .OrderByDescending(t => t.LastMessageDate).ToList());

    public Task<List<SecureMessageThreadSummary>> GetUnreadByPatientAsync()
        => Task.FromResult(_state.State.Threads
            .Where(t => t.HasUnreadPatient)
            .OrderByDescending(t => t.LastMessageDate).ToList());

    public Task<List<SecureMessageThreadSummary>> GetUnreadByProviderAsync()
        => Task.FromResult(_state.State.Threads
            .Where(t => t.HasUnreadProvider)
            .OrderByDescending(t => t.LastMessageDate).ToList());
}

/// <summary>
/// System-wide secure message queue for providers — threads needing attention.
/// </summary>
public class SecureMessageQueueGrain : Grain, ISecureMessageQueueGrain
{
    private readonly IPersistentState<SecureMessageQueueState> _state;

    public SecureMessageQueueGrain(
        [PersistentState("secureMessageQueueState", "secureMessageQueueStore")] IPersistentState<SecureMessageQueueState> state)
    {
        _state = state;
    }

    public async Task AddThreadAsync(SecureMessageThreadSummary summary)
    {
        _state.State.Threads.RemoveAll(t => t.ThreadId == summary.ThreadId);
        _state.State.Threads.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateThreadAsync(SecureMessageThreadSummary summary)
    {
        _state.State.Threads.RemoveAll(t => t.ThreadId == summary.ThreadId);
        _state.State.Threads.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveThreadAsync(string threadId)
    {
        _state.State.Threads.RemoveAll(t => t.ThreadId == threadId);
        await _state.WriteStateAsync();
    }

    public Task<List<SecureMessageThreadSummary>> GetUnreadThreadsAsync()
        => Task.FromResult(_state.State.Threads
            .Where(t => t.HasUnreadProvider)
            .OrderByDescending(t => t.LastMessageDate).ToList());

    public Task<List<SecureMessageThreadSummary>> GetAllActiveThreadsAsync()
        => Task.FromResult(_state.State.Threads
            .Where(t => t.Status == "open")
            .OrderByDescending(t => t.LastMessageDate).ToList());
}
