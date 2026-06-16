// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Notification/Alert grain.
/// Mirrors VistA ALERT file (XTV 8A) and CPRS ORB notification types.
/// Grain key: the XQAID (alert ID).
/// </summary>
[TestFixture]
public class NotificationGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private INotificationGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<INotificationGrain>($"NOTIF-{Guid.NewGuid()}");

    [Test]
    public async Task NotificationGrain_Create_PersistsAllFields()
    {
        INotificationGrain grain = NewGrain();

        await grain.CreateNotificationAsync(
            "PATIENT-001", 10, "Critical Lab Result",
            "USER-001", "Dr. Adams",
            "LAB", "Potassium 6.8 mEq/L — CRITICAL HIGH",
            "REVIEW_LAB_RESULT", true, null);

        NotificationState state = await grain.GetNotificationAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.NotificationType, Is.EqualTo(10));
        Assert.That(state.NotificationTypeText, Is.EqualTo("Critical Lab Result"));
        Assert.That(state.RecipientId, Is.EqualTo("USER-001"));
        Assert.That(state.RecipientName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.SendingPackage, Is.EqualTo("LAB"));
        Assert.That(state.MessageText, Does.Contain("Potassium"));
        Assert.That(state.IsCritical, Is.True);
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task NotificationGrain_ProcessAlert_SetsProcessedFlag()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-002", 20, "Abnormal Lab",
            "USER-002", "Dr. Baker",
            "LAB", "Hemoglobin 6.2 — LOW",
            null, false, null);

        DateTime processedTime = DateTime.UtcNow;
        await grain.ProcessAlertAsync(processedTime, "USER-002");

        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.Status, Is.EqualTo("PROCESSED"));
        Assert.That(state.ProcessedDateTime, Is.EqualTo(processedTime));
        Assert.That(state.ProcessedByUserId, Is.EqualTo("USER-002"));
    }

    [Test]
    public async Task NotificationGrain_DeleteAlert_SetsDeletedFlag()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-003", 30, "Expiring Order",
            "USER-003", "Dr. Carter",
            "ORDER", "Heparin drip expires in 24 hours",
            null, false, null);

        await grain.DeleteAlertAsync("USER-003");

        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.Status, Is.EqualTo("DELETED"));
    }

    [Test]
    public async Task NotificationGrain_ForwardAlert_RecordsForwardDetails()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-004", 10, "Critical Lab Result",
            "USER-001", "Dr. Adams",
            "LAB", "INR 8.2 — CRITICAL",
            null, true, null);

        await grain.ForwardAlertAsync(
            "USER-005", "Dr. Ellis",
            "MANDATORY", "Please handle this urgently",
            "USER-001");

        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.ForwardHistory.Last().ToRecipientId, Is.EqualTo("USER-005"));
        Assert.That(state.ForwardHistory.Last().ToRecipientName, Is.EqualTo("Dr. Ellis"));
        Assert.That(state.ForwardHistory.Last().ForwardType, Is.EqualTo("MANDATORY"));
    }

    [Test]
    public async Task NotificationGrain_RenewAlert_ClearsProcessedFlag()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-005", 40, "New Order",
            "USER-006", "Dr. Foster",
            "ORDER", "New medication order requires co-signature",
            null, false, null);

        await grain.ProcessAlertAsync(DateTime.UtcNow, "USER-006");
        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.Status, Is.EqualTo("PROCESSED"));

        await grain.RenewAlertAsync("USER-007");
        state = await grain.GetNotificationAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task NotificationGrain_AddFollowUpComment_AppendsComment()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-006", 50, "Patient Data Change",
            "USER-008", "Dr. Grant",
            "REGISTRATION", "Patient address updated",
            null, false, null);

        await grain.AddFollowUpCommentAsync("Verified with patient by phone", "USER-008");
        await grain.AddFollowUpCommentAsync("Updated in chart", "USER-009");

        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.FollowUpComments, Has.Count.EqualTo(2));
        Assert.That(state.FollowUpComments.Any(c => c.Contains("phone")), Is.True);
    }

    [Test]
    public async Task NotificationGrain_SetFollowUpText_UpdatesActionText()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-007", 60, "Unsigned Note",
            "USER-010", "Dr. Harris",
            "TIU", "Consult note requires signature",
            "SIGN_DOCUMENT", false, null);

        await grain.SetFollowUpTextAsync("Sign document in TIU");

        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.FollowUpText, Is.EqualTo("Sign document in TIU"));
    }

    [Test]
    public async Task NotificationGrain_IsSmartAlert_ReturnsTrueWhenMarkerInMessage()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-008", 10, "Critical Lab",
            "USER-011", "Dr. Ingram",
            "LAB", "Troponin 4.2 — CRITICAL<--- SMART ALERT INFO --->LAB;ORDER-001;RESULT-001",
            null, true, null);

        bool isSmart = await grain.IsSmartAlertAsync();
        Assert.That(isSmart, Is.True);
    }

    [Test]
    public async Task NotificationGrain_IsSmartAlert_ReturnsFalseWhenNoXqaData()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-009", 20, "Routine Lab",
            "USER-012", "Dr. James",
            "LAB", "CBC complete",
            null, false, null);

        bool isSmart = await grain.IsSmartAlertAsync();
        Assert.That(isSmart, Is.False);
    }

    [Test]
    public async Task NotificationGrain_CriticalAlert_CanBeProcessedAndRenewed()
    {
        INotificationGrain grain = NewGrain();
        await grain.CreateNotificationAsync(
            "PATIENT-010", 10, "Critical Lab",
            "USER-013", "Dr. Kelly",
            "LAB", "Glucose 32 — CRITICAL LOW",
            "REVIEW_LAB_RESULT", true, null);

        // Process
        await grain.ProcessAlertAsync(DateTime.UtcNow, "USER-013");
        NotificationState state = await grain.GetNotificationAsync();
        Assert.That(state.Status, Is.EqualTo("PROCESSED"));
        Assert.That(state.IsCritical, Is.True, "Critical flag should remain after processing");

        // Renew
        await grain.RenewAlertAsync("USER-014");
        state = await grain.GetNotificationAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"), "Should be active after renewal");
        Assert.That(state.IsCritical, Is.True, "Critical flag should remain after renewal");
    }
}
