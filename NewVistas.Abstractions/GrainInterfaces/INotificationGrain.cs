// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Notification (Alert) Grain Interface — mirrors VistA ALERT file (XTV 8A)
/// and the notification types driven by OR CPRS alert logic (ORB notifications).
///
/// Each notification is an independent grain keyed by its alert ID.
/// MUMPS references: ORB*.m, XQALERT.m
/// </summary>
public interface INotificationGrain : IGrainWithStringKey
{
    /// <summary>Gets the full notification state.</summary>
    Task<GrainStates.NotificationState> GetNotificationAsync();

    /// <summary>
    /// Creates a new notification/alert.
    /// </summary>
    Task CreateNotificationAsync(
        string patientId,
        int notificationType,
        string notificationTypeText,
        string recipientId,
        string recipientName,
        string? sendingPackage,
        string? messageText,
        string? followUpAction,
        bool isCritical,
        string? xqaData);

    /// <summary>Marks the alert as processed/read by the recipient.</summary>
    Task ProcessAlertAsync(DateTime processedDateTime, string processedByUserId);

    /// <summary>Deletes/dismisses the alert.</summary>
    Task DeleteAlertAsync(string deletedByUserId);

    /// <summary>
    /// Forwards the alert to another recipient.
    /// </summary>
    Task ForwardAlertAsync(
        string toRecipientId,
        string toRecipientName,
        string forwardType,
        string? comment,
        string forwardedByUserId);

    /// <summary>Renews (re-triggers) a dismissed alert.</summary>
    Task RenewAlertAsync(string renewedByUserId);

    /// <summary>Records a follow-up comment on the alert.</summary>
    Task AddFollowUpCommentAsync(string comment, string byUserId);

    /// <summary>
    /// Updates the follow-up text (action text displayed to user).
    /// </summary>
    Task SetFollowUpTextAsync(string followUpText);

    /// <summary>Returns true if this is a SMART ALERT (contains extended info).</summary>
    Task<bool> IsSmartAlertAsync();
}
