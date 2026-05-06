// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Notification (Alert) Grain — VistA ALERT / ORB NOTIFICATION
/// Grain key is the XQAID (alert ID).
/// </summary>
public class NotificationGrain : Grain, INotificationGrain
{
    private readonly IPersistentState<NotificationState> _state;

    public NotificationGrain(
        [PersistentState("notificationState", "notificationStore")] IPersistentState<NotificationState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AlertId))
        {
            _state.State.AlertId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<NotificationState> GetNotificationAsync() => Task.FromResult(_state.State);

    public async Task CreateNotificationAsync(
        string patientId,
        int notificationType,
        string notificationTypeText,
        string recipientId,
        string recipientName,
        string? sendingPackage,
        string? messageText,
        string? followUpAction,
        bool isCritical,
        string? xqaData)
    {
        _state.State.PatientId = patientId;
        _state.State.NotificationType = notificationType;
        _state.State.NotificationTypeText = notificationTypeText;
        _state.State.RecipientId = recipientId;
        _state.State.RecipientName = recipientName;
        _state.State.SendingPackage = sendingPackage;
        _state.State.MessageText = messageText;
        _state.State.FollowUpAction = followUpAction;
        _state.State.IsCritical = isCritical;
        _state.State.XqaData = xqaData;
        _state.State.Status = "ACTIVE";
        _state.State.AlertDateTime = DateTime.UtcNow;
        // Smart alerts contain an extended info block delimited by <--- SMART ALERT INFO --->
        _state.State.IsSmartAlert = messageText?.Contains("<--- SMART ALERT INFO --->") ?? false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ProcessAlertAsync(DateTime processedDateTime, string processedByUserId)
    {
        _state.State.Status = "PROCESSED";
        _state.State.ProcessedDateTime = processedDateTime;
        _state.State.ProcessedByUserId = processedByUserId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeleteAlertAsync(string deletedByUserId)
    {
        _state.State.Status = "DELETED";
        _state.State.DeletedDateTime = DateTime.UtcNow;
        _state.State.DeletedByUserId = deletedByUserId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ForwardAlertAsync(
        string toRecipientId,
        string toRecipientName,
        string forwardType,
        string? comment,
        string forwardedByUserId)
    {
        _state.State.ForwardHistory.Add(new AlertForwardRecord
        {
            ToRecipientId = toRecipientId,
            ToRecipientName = toRecipientName,
            ForwardType = forwardType,
            Comment = comment,
            ForwardedByUserId = forwardedByUserId,
            ForwardedDateTime = DateTime.UtcNow
        });
        _state.State.Status = "FORWARDED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RenewAlertAsync(string renewedByUserId)
    {
        _state.State.Status = "ACTIVE";
        _state.State.AlertDateTime = DateTime.UtcNow;
        _state.State.ProcessedDateTime = null;
        _state.State.ProcessedByUserId = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddFollowUpCommentAsync(string comment, string byUserId)
    {
        _state.State.FollowUpComments.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} [{byUserId}]: {comment}");
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetFollowUpTextAsync(string followUpText)
    {
        _state.State.FollowUpText = followUpText;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsSmartAlertAsync() => Task.FromResult(_state.State.IsSmartAlert);
}
