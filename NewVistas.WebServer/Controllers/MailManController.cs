// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// MailMan — Internal messaging system controller.
/// Exposes message sending, mailbox management, mail groups, and bulletins.
/// Maps to VistA XM package, Files #3.6 (Bulletin), #3.7 (Mailbox), #3.8 (Mail Group), #3.9 (Message).
///
/// Routes:
///   POST api/mailman/messages                                    - Create and send a message
///   GET  api/mailman/messages/{messageId}                        - Get a specific message
///   POST api/mailman/messages/{messageId}/read                   - Mark message as read
///   POST api/mailman/messages/{messageId}/unread                 - Mark message as unread
///   GET  api/mailman/users/{userId}/inbox                        - Get user inbox
///   GET  api/mailman/users/{userId}/sent                         - Get user sent items
///   GET  api/mailman/users/{userId}/deleted                      - Get user deleted items
///   GET  api/mailman/users/{userId}/unread-count                 - Get unread count
///   POST api/mailman/users/{userId}/messages/{messageId}/delete  - Move message to deleted
///   POST api/mailman/users/{userId}/messages/{messageId}/restore - Restore from deleted
///   DELETE api/mailman/users/{userId}/deleted                    - Purge all deleted items
///   POST api/mailman/users/{userId}/folders                      - Create custom folder
///   POST api/mailman/users/{userId}/messages/{messageId}/move    - Move to folder
///   GET  api/mailman/groups                                      - List all mail groups
///   POST api/mailman/groups                                      - Create mail group
///   GET  api/mailman/groups/{groupName}                          - Get mail group
///   POST api/mailman/groups/{groupName}/members                  - Add member
///   DELETE api/mailman/groups/{groupName}/members/{userId}       - Remove member
///   GET  api/mailman/bulletins/{bulletinName}                     - Get bulletin
///   POST api/mailman/bulletins                                   - Create bulletin
///   POST api/mailman/bulletins/{bulletinName}/fire                - Fire bulletin
///   POST api/mailman/demo/load                                   - Load demo data
/// </summary>
[Authorize]
[ApiController]
[Route("api/mailman")]
[Produces("application/json")]
public class MailManController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<MailManController> _log;

    public MailManController(IGrainFactory gf, ILogger<MailManController> log)
    {
        _gf = gf;
        _log = log;
    }

    private IMailMessageGrain MessageGrain(string messageId) =>
        _gf.GetGrain<IMailMessageGrain>(messageId);

    private IUserMailboxGrain MailboxGrain(string userId) =>
        _gf.GetGrain<IUserMailboxGrain>($"MAILBOX:{userId}");

    private IMailGroupGrain GroupGrain(string groupName) =>
        _gf.GetGrain<IMailGroupGrain>($"MAILGRP:{groupName}");

    private IMailGroupIndexGrain GroupIndexGrain() =>
        _gf.GetGrain<IMailGroupIndexGrain>("MAILGRP-INDEX");

    private IBulletinGrain BulletinGrainRef(string bulletinName) =>
        _gf.GetGrain<IBulletinGrain>($"BULLETIN:{bulletinName}");

    // ─── Message Endpoints ──────────────────────────────────────────────

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] MmSendMessageRequest request)
    {
        try
        {
            string messageId = $"MAIL-{Guid.NewGuid():N}";
            IMailMessageGrain msgGrain = MessageGrain(messageId);

            List<MailRecipient> recipients = request.Recipients?.Select(r => new MailRecipient
            {
                RecipientId = r.RecipientId,
                RecipientName = r.RecipientName,
                RecipientType = r.RecipientType ?? "USER"
            }).ToList() ?? new();

            List<MailRecipient> ccRecipients = request.CcRecipients?.Select(r => new MailRecipient
            {
                RecipientId = r.RecipientId,
                RecipientName = r.RecipientName,
                RecipientType = r.RecipientType ?? "USER"
            }).ToList() ?? new();

            await msgGrain.CreateMessageAsync(
                messageId,
                request.Subject ?? string.Empty,
                request.Body ?? string.Empty,
                request.SenderId ?? string.Empty,
                request.SenderName ?? string.Empty,
                recipients,
                ccRecipients,
                request.Priority ?? "ROUTINE",
                request.IsConfidential,
                request.InReplyToMessageId);

            await msgGrain.SendAsync();

            // Build mailbox entry
            MailboxEntry sentEntry = new()
            {
                MessageId = messageId,
                Subject = request.Subject ?? string.Empty,
                SenderName = request.SenderName ?? string.Empty,
                SentDateTime = DateTime.UtcNow,
                IsRead = true,
                Priority = request.Priority ?? "ROUTINE",
                FolderId = "SENT",
                IsConfidential = request.IsConfidential
            };

            // Add to sender's sent items
            await MailboxGrain(request.SenderId ?? string.Empty).AddSentEntryAsync(sentEntry);

            // Deliver to each recipient's inbox
            MailboxEntry inboxEntry = new()
            {
                MessageId = messageId,
                Subject = request.Subject ?? string.Empty,
                SenderName = request.SenderName ?? string.Empty,
                SentDateTime = DateTime.UtcNow,
                IsRead = false,
                Priority = request.Priority ?? "ROUTINE",
                FolderId = "INBOX",
                IsConfidential = request.IsConfidential
            };

            foreach (MailRecipient recipient in recipients.Concat(ccRecipients))
            {
                if (recipient.RecipientType == "MAIL_GROUP")
                {
                    // Expand mail group and deliver to each member
                    IMailGroupGrain groupGrain = GroupGrain(recipient.RecipientId);
                    List<MailGroupMember> members = await groupGrain.GetMembersAsync();
                    foreach (MailGroupMember member in members)
                    {
                        await MailboxGrain(member.UserId).AddInboxEntryAsync(inboxEntry);
                    }
                }
                else
                {
                    await MailboxGrain(recipient.RecipientId).AddInboxEntryAsync(inboxEntry);
                }
            }

            return Created($"api/mailman/messages/{messageId}", new { MessageId = messageId });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error sending message from {SenderId}", request.SenderId);
            return StatusCode(500, "Error sending message.");
        }
    }

    [HttpGet("messages/{messageId}")]
    public async Task<IActionResult> GetMessage(string messageId)
    {
        try
        {
            MailMessageState msg = await MessageGrain(messageId).GetMessageAsync();
            return Ok(ToMessageDto(msg));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving message {MessageId}", messageId);
            return StatusCode(500, "Error retrieving message.");
        }
    }

    [HttpPost("messages/{messageId}/read")]
    public async Task<IActionResult> MarkRead(string messageId, [FromBody] MmMarkReadRequest request)
    {
        try
        {
            await MessageGrain(messageId).MarkReadAsync(request.UserId ?? string.Empty);
            await MailboxGrain(request.UserId ?? string.Empty).MarkMessageReadAsync(messageId);
            return Ok(new { messageId, Message = "Message marked as read." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error marking message {MessageId} as read", messageId);
            return StatusCode(500, "Error marking message as read.");
        }
    }

    [HttpPost("messages/{messageId}/unread")]
    public async Task<IActionResult> MarkUnread(string messageId, [FromBody] MmMarkReadRequest request)
    {
        try
        {
            await MessageGrain(messageId).MarkUnreadAsync(request.UserId ?? string.Empty);
            await MailboxGrain(request.UserId ?? string.Empty).MarkMessageUnreadAsync(messageId);
            return Ok(new { messageId, Message = "Message marked as unread." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error marking message {MessageId} as unread", messageId);
            return StatusCode(500, "Error marking message as unread.");
        }
    }

    // ─── Mailbox Endpoints ──────────────────────────────────────────────

    [HttpGet("users/{userId}/inbox")]
    public async Task<IActionResult> GetInbox(string userId)
    {
        try
        {
            List<MailboxEntry> entries = await MailboxGrain(userId).GetInboxAsync();
            return Ok(entries.Select(ToEntryDto));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving inbox for user {UserId}", userId);
            return StatusCode(500, "Error retrieving inbox.");
        }
    }

    [HttpGet("users/{userId}/sent")]
    public async Task<IActionResult> GetSent(string userId)
    {
        try
        {
            List<MailboxEntry> entries = await MailboxGrain(userId).GetSentItemsAsync();
            return Ok(entries.Select(ToEntryDto));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving sent items for user {UserId}", userId);
            return StatusCode(500, "Error retrieving sent items.");
        }
    }

    [HttpGet("users/{userId}/deleted")]
    public async Task<IActionResult> GetDeleted(string userId)
    {
        try
        {
            List<MailboxEntry> entries = await MailboxGrain(userId).GetDeletedItemsAsync();
            return Ok(entries.Select(ToEntryDto));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving deleted items for user {UserId}", userId);
            return StatusCode(500, "Error retrieving deleted items.");
        }
    }

    [HttpGet("users/{userId}/unread-count")]
    public async Task<IActionResult> GetUnreadCount(string userId)
    {
        try
        {
            int count = await MailboxGrain(userId).GetUnreadCountAsync();
            return Ok(new { UserId = userId, UnreadCount = count });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving unread count for user {UserId}", userId);
            return StatusCode(500, "Error retrieving unread count.");
        }
    }

    [HttpPost("users/{userId}/messages/{messageId}/delete")]
    public async Task<IActionResult> DeleteMessage(string userId, string messageId)
    {
        try
        {
            await MailboxGrain(userId).MoveToDeletedAsync(messageId);
            return Ok(new { messageId, Message = "Message moved to deleted." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
            return StatusCode(500, "Error deleting message.");
        }
    }

    [HttpPost("users/{userId}/messages/{messageId}/restore")]
    public async Task<IActionResult> RestoreMessage(string userId, string messageId)
    {
        try
        {
            await MailboxGrain(userId).RestoreFromDeletedAsync(messageId);
            return Ok(new { messageId, Message = "Message restored from deleted." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error restoring message {MessageId} for user {UserId}", messageId, userId);
            return StatusCode(500, "Error restoring message.");
        }
    }

    [HttpDelete("users/{userId}/deleted")]
    public async Task<IActionResult> PurgeDeleted(string userId)
    {
        try
        {
            await MailboxGrain(userId).PurgeDeletedAsync();
            return Ok(new { UserId = userId, Message = "Deleted items purged." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error purging deleted items for user {UserId}", userId);
            return StatusCode(500, "Error purging deleted items.");
        }
    }

    [HttpPost("users/{userId}/folders")]
    public async Task<IActionResult> CreateFolder(string userId, [FromBody] MmCreateFolderRequest request)
    {
        try
        {
            string folderId = $"FOLDER-{Guid.NewGuid():N}";
            await MailboxGrain(userId).CreateFolderAsync(folderId, request.FolderName ?? string.Empty);
            return Created($"api/mailman/users/{userId}/folders/{folderId}",
                new { FolderId = folderId, Message = "Folder created." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error creating folder for user {UserId}", userId);
            return StatusCode(500, "Error creating folder.");
        }
    }

    [HttpPost("users/{userId}/messages/{messageId}/move")]
    public async Task<IActionResult> MoveToFolder(string userId, string messageId, [FromBody] MmMoveToFolderRequest request)
    {
        try
        {
            await MailboxGrain(userId).MoveToFolderAsync(messageId, request.FolderId ?? string.Empty);
            return Ok(new { messageId, FolderId = request.FolderId, Message = "Message moved." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error moving message {MessageId} to folder for user {UserId}", messageId, userId);
            return StatusCode(500, "Error moving message.");
        }
    }

    // ─── Mail Group Endpoints ───────────────────────────────────────────

    [HttpGet("groups")]
    public async Task<IActionResult> GetAllGroups()
    {
        try
        {
            List<MailGroupIndexEntry> groups = await GroupIndexGrain().GetAllGroupsAsync();
            return Ok(groups.Select(g => new MmGroupSummaryDto
            {
                Name = g.Name,
                Description = g.Description,
                MemberCount = g.MemberCount,
                IsActive = g.IsActive
            }));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving mail groups");
            return StatusCode(500, "Error retrieving mail groups.");
        }
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] MmCreateGroupRequest request)
    {
        try
        {
            string groupName = request.GroupName ?? string.Empty;
            IMailGroupGrain groupGrain = GroupGrain(groupName);
            await groupGrain.CreateGroupAsync(
                groupName,
                request.Description ?? string.Empty,
                request.OrganizerId ?? string.Empty,
                request.OrganizerName ?? string.Empty,
                request.MembershipType ?? "OPEN");

            await GroupIndexGrain().AddOrUpdateGroupAsync(new MailGroupIndexEntry
            {
                Name = groupName,
                Description = request.Description ?? string.Empty,
                MemberCount = 0,
                IsActive = true
            });

            return Created($"api/mailman/groups/{groupName}",
                new { GroupName = groupName, Message = "Mail group created." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error creating mail group {GroupName}", request.GroupName);
            return StatusCode(500, "Error creating mail group.");
        }
    }

    [HttpGet("groups/{groupName}")]
    public async Task<IActionResult> GetGroup(string groupName)
    {
        try
        {
            MailGroupState grp = await GroupGrain(groupName).GetGroupAsync();
            return Ok(new MmGroupDetailDto
            {
                GroupName = grp.GroupName,
                Description = grp.Description,
                OrganizerId = grp.OrganizerId,
                OrganizerName = grp.OrganizerName,
                MembershipType = grp.MembershipType,
                IsActive = grp.IsActive,
                Members = grp.Members.Select(m => new MmGroupMemberDto
                {
                    UserId = m.UserId,
                    UserName = m.UserName,
                    Role = m.Role,
                    JoinedDate = m.JoinedDate
                }).ToList(),
                CreatedDate = grp.CreatedDate
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving mail group {GroupName}", groupName);
            return StatusCode(500, "Error retrieving mail group.");
        }
    }

    [HttpPost("groups/{groupName}/members")]
    public async Task<IActionResult> AddMember(string groupName, [FromBody] MmAddMemberRequest request)
    {
        try
        {
            IMailGroupGrain groupGrain = GroupGrain(groupName);
            await groupGrain.AddMemberAsync(
                request.UserId ?? string.Empty,
                request.UserName ?? string.Empty,
                request.Role ?? "MEMBER");

            // Update index with new member count
            MailGroupState grp = await groupGrain.GetGroupAsync();
            await GroupIndexGrain().AddOrUpdateGroupAsync(new MailGroupIndexEntry
            {
                Name = grp.GroupName,
                Description = grp.Description,
                MemberCount = grp.Members.Count,
                IsActive = grp.IsActive
            });

            return Created($"api/mailman/groups/{groupName}/members/{request.UserId}",
                new { GroupName = groupName, UserId = request.UserId, Message = "Member added." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding member to mail group {GroupName}", groupName);
            return StatusCode(500, "Error adding member.");
        }
    }

    [HttpDelete("groups/{groupName}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(string groupName, string userId)
    {
        try
        {
            IMailGroupGrain groupGrain = GroupGrain(groupName);
            await groupGrain.RemoveMemberAsync(userId);

            // Update index with new member count
            MailGroupState grp = await groupGrain.GetGroupAsync();
            await GroupIndexGrain().AddOrUpdateGroupAsync(new MailGroupIndexEntry
            {
                Name = grp.GroupName,
                Description = grp.Description,
                MemberCount = grp.Members.Count,
                IsActive = grp.IsActive
            });

            return Ok(new { GroupName = groupName, UserId = userId, Message = "Member removed." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error removing member {UserId} from mail group {GroupName}", userId, groupName);
            return StatusCode(500, "Error removing member.");
        }
    }

    // ─── Bulletin Endpoints ─────────────────────────────────────────────

    [HttpGet("bulletins/{bulletinName}")]
    public async Task<IActionResult> GetBulletin(string bulletinName)
    {
        try
        {
            BulletinState bulletin = await BulletinGrainRef(bulletinName).GetBulletinAsync();
            return Ok(new MmBulletinDto
            {
                BulletinName = bulletin.BulletinName,
                Description = bulletin.Description,
                AssociatedMailGroup = bulletin.AssociatedMailGroup,
                TriggerEvent = bulletin.TriggerEvent,
                MessageTemplate = bulletin.MessageTemplate,
                IsActive = bulletin.IsActive,
                FireCount = bulletin.FireCount,
                LastFiredDateTime = bulletin.LastFiredDateTime,
                CreatedDate = bulletin.CreatedDate
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving bulletin {BulletinName}", bulletinName);
            return StatusCode(500, "Error retrieving bulletin.");
        }
    }

    [HttpPost("bulletins")]
    public async Task<IActionResult> CreateBulletin([FromBody] MmCreateBulletinRequest request)
    {
        try
        {
            string bulletinName = request.BulletinName ?? string.Empty;
            await BulletinGrainRef(bulletinName).CreateBulletinAsync(
                bulletinName,
                request.Description ?? string.Empty,
                request.AssociatedMailGroup ?? string.Empty,
                request.TriggerEvent ?? string.Empty,
                request.MessageTemplate);

            return Created($"api/mailman/bulletins/{bulletinName}",
                new { BulletinName = bulletinName, Message = "Bulletin created." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error creating bulletin {BulletinName}", request.BulletinName);
            return StatusCode(500, "Error creating bulletin.");
        }
    }

    [HttpPost("bulletins/{bulletinName}/fire")]
    public async Task<IActionResult> FireBulletin(string bulletinName)
    {
        try
        {
            await BulletinGrainRef(bulletinName).FireBulletinAsync();
            return Ok(new { BulletinName = bulletinName, Message = "Bulletin fired." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error firing bulletin {BulletinName}", bulletinName);
            return StatusCode(500, "Error firing bulletin.");
        }
    }

    // ─── Demo Load ──────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo([FromQuery] string? userId)
    {
        try
        {
            string uid = userId ?? "USER-DEMO-001";
            DateTime now = DateTime.UtcNow;

            // Create a mail group
            string groupName = "PHARMACY-ALERTS";
            IMailGroupGrain pharmacyGroup = GroupGrain(groupName);
            await pharmacyGroup.CreateGroupAsync(
                groupName,
                "Pharmacy alert notifications",
                uid,
                "Dr. Demo User",
                "OPEN");
            await pharmacyGroup.AddMemberAsync(uid, "Dr. Demo User", "ORGANIZER");
            await pharmacyGroup.AddMemberAsync("USER-PHARM-001", "Pharmacist Jones", "MEMBER");
            await pharmacyGroup.AddMemberAsync("USER-PHARM-002", "Pharmacist Smith", "MEMBER");

            await GroupIndexGrain().AddOrUpdateGroupAsync(new MailGroupIndexEntry
            {
                Name = groupName,
                Description = "Pharmacy alert notifications",
                MemberCount = 3,
                IsActive = true
            });

            // Create sample messages
            string[] subjects = [
                "Lab Results Available — Critical Value",
                "Consult Request: Cardiology",
                "Discharge Summary Ready for Cosign",
                "Pharmacy: Drug Interaction Alert",
                "Staff Meeting Tomorrow 0800"
            ];
            string[] bodies = [
                "Patient SMITH,JOHN has a critical Potassium value of 6.8 mEq/L. Please review immediately.",
                "A consult has been requested for patient DOE,JANE to Cardiology for evaluation of new-onset atrial fibrillation.",
                "A discharge summary for patient BROWN,ROBERT is ready for your cosignature in TIU.",
                "A potential drug interaction has been identified between Warfarin and Aspirin for patient WILSON,MARY.",
                "Reminder: All-hands staff meeting tomorrow at 0800 in Conference Room B. Attendance is mandatory."
            ];
            string[] senders = [
                "Lab System", "Dr. Cardiology", "Dr. Hospitalist", "Pharmacy System", "Chief of Staff"
            ];
            string[] senderIds = [
                "SYSTEM-LAB", "USER-CARD-001", "USER-HOSP-001", "SYSTEM-PHARM", "USER-COS-001"
            ];
            string[] priorities = ["URGENT", "HIGH", "ROUTINE", "HIGH", "ROUTINE"];

            for (int i = 0; i < subjects.Length; i++)
            {
                string messageId = $"MAIL-{Guid.NewGuid():N}";
                IMailMessageGrain msgGrain = MessageGrain(messageId);

                List<MailRecipient> recipients = [new MailRecipient
                {
                    RecipientId = uid,
                    RecipientName = "Dr. Demo User",
                    RecipientType = "USER"
                }];

                await msgGrain.CreateMessageAsync(
                    messageId, subjects[i], bodies[i],
                    senderIds[i], senders[i],
                    recipients, null,
                    priorities[i], i == 0, null);
                await msgGrain.SendAsync();

                MailboxEntry entry = new()
                {
                    MessageId = messageId,
                    Subject = subjects[i],
                    SenderName = senders[i],
                    SentDateTime = now.AddHours(-i * 2),
                    IsRead = i > 2,
                    Priority = priorities[i],
                    FolderId = "INBOX",
                    IsConfidential = i == 0
                };
                await MailboxGrain(uid).AddInboxEntryAsync(entry);
            }

            // Create a sent message
            string sentMsgId = $"MAIL-{Guid.NewGuid():N}";
            IMailMessageGrain sentGrain = MessageGrain(sentMsgId);
            await sentGrain.CreateMessageAsync(
                sentMsgId, "Re: Lab Results Available",
                "Acknowledged. Will review patient SMITH,JOHN's labs immediately.",
                uid, "Dr. Demo User",
                [new MailRecipient { RecipientId = "SYSTEM-LAB", RecipientName = "Lab System", RecipientType = "USER" }],
                null, "ROUTINE", false, null);
            await sentGrain.SendAsync();
            await MailboxGrain(uid).AddSentEntryAsync(new MailboxEntry
            {
                MessageId = sentMsgId,
                Subject = "Re: Lab Results Available",
                SenderName = "Dr. Demo User",
                SentDateTime = now.AddMinutes(-30),
                IsRead = true,
                Priority = "ROUTINE",
                FolderId = "SENT"
            });

            // Create a bulletin
            await BulletinGrainRef("STAT-LAB-RESULT").CreateBulletinAsync(
                "STAT-LAB-RESULT",
                "Fires when a STAT lab result is finalized",
                groupName,
                "STAT_RESULT",
                "STAT lab result available for review: {{PatientName}} — {{TestName}}: {{Value}}");

            return Ok(new
            {
                Message = "Demo MailMan data loaded.",
                UserId = uid,
                InboxMessages = subjects.Length,
                SentMessages = 1,
                MailGroups = 1,
                Bulletins = 1
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error loading MailMan demo data for user {UserId}", userId);
            return StatusCode(500, "Error loading demo data.");
        }
    }

    // ─── DTO Projections ────────────────────────────────────────────────

    private static MmMessageDto ToMessageDto(MailMessageState s) => new()
    {
        MessageId = s.MessageId,
        Subject = s.Subject,
        Body = s.Body,
        SenderId = s.SenderId,
        SenderName = s.SenderName,
        SentDateTime = s.SentDateTime,
        Priority = s.Priority,
        IsConfidential = s.IsConfidential,
        Status = s.Status,
        InReplyToMessageId = s.InReplyToMessageId,
        AttachmentDescriptions = s.AttachmentDescriptions,
        Recipients = s.Recipients.Select(r => new MmRecipientDto
        {
            RecipientId = r.RecipientId,
            RecipientName = r.RecipientName,
            RecipientType = r.RecipientType,
            IsRead = r.IsRead,
            ReadDateTime = r.ReadDateTime
        }).ToList(),
        CcRecipients = s.CcRecipients.Select(r => new MmRecipientDto
        {
            RecipientId = r.RecipientId,
            RecipientName = r.RecipientName,
            RecipientType = r.RecipientType,
            IsRead = r.IsRead,
            ReadDateTime = r.ReadDateTime
        }).ToList(),
        CreatedDate = s.CreatedDate
    };

    private static MmMailboxEntryDto ToEntryDto(MailboxEntry e) => new()
    {
        MessageId = e.MessageId,
        Subject = e.Subject,
        SenderName = e.SenderName,
        SentDateTime = e.SentDateTime,
        IsRead = e.IsRead,
        Priority = e.Priority,
        FolderId = e.FolderId,
        IsConfidential = e.IsConfidential
    };
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record MmSendMessageRequest
{
    public string? SenderId { get; init; }
    public string? SenderName { get; init; }
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public List<MmRecipientInput>? Recipients { get; init; }
    public List<MmRecipientInput>? CcRecipients { get; init; }
    public string? Priority { get; init; }
    public bool IsConfidential { get; init; }
    public string? InReplyToMessageId { get; init; }
}

public record MmRecipientInput
{
    public string RecipientId { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string? RecipientType { get; init; }
}

public record MmMarkReadRequest
{
    public string? UserId { get; init; }
}

public record MmCreateFolderRequest
{
    public string? FolderName { get; init; }
}

public record MmMoveToFolderRequest
{
    public string? FolderId { get; init; }
}

public record MmCreateGroupRequest
{
    public string? GroupName { get; init; }
    public string? Description { get; init; }
    public string? OrganizerId { get; init; }
    public string? OrganizerName { get; init; }
    public string? MembershipType { get; init; }
}

public record MmAddMemberRequest
{
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Role { get; init; }
}

public record MmCreateBulletinRequest
{
    public string? BulletinName { get; init; }
    public string? Description { get; init; }
    public string? AssociatedMailGroup { get; init; }
    public string? TriggerEvent { get; init; }
    public string? MessageTemplate { get; init; }
}

// ─── Response DTOs ────────────────────────────────────────────────────────────

public record MmMessageDto
{
    public string MessageId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string SenderId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public DateTime SentDateTime { get; init; }
    public string Priority { get; init; } = "ROUTINE";
    public bool IsConfidential { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? InReplyToMessageId { get; init; }
    public List<string> AttachmentDescriptions { get; init; } = [];
    public List<MmRecipientDto> Recipients { get; init; } = [];
    public List<MmRecipientDto> CcRecipients { get; init; } = [];
    public DateTime CreatedDate { get; init; }
}

public record MmRecipientDto
{
    public string RecipientId { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientType { get; init; } = "USER";
    public bool IsRead { get; init; }
    public DateTime? ReadDateTime { get; init; }
}

public record MmMailboxEntryDto
{
    public string MessageId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public DateTime SentDateTime { get; init; }
    public bool IsRead { get; init; }
    public string Priority { get; init; } = "ROUTINE";
    public string FolderId { get; init; } = "INBOX";
    public bool IsConfidential { get; init; }
}

public record MmGroupSummaryDto
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public bool IsActive { get; init; }
}

public record MmGroupDetailDto
{
    public string GroupName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OrganizerId { get; init; } = string.Empty;
    public string OrganizerName { get; init; } = string.Empty;
    public string MembershipType { get; init; } = "OPEN";
    public bool IsActive { get; init; }
    public List<MmGroupMemberDto> Members { get; init; } = [];
    public DateTime CreatedDate { get; init; }
}

public record MmGroupMemberDto
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Role { get; init; } = "MEMBER";
    public DateTime JoinedDate { get; init; }
}

public record MmBulletinDto
{
    public string BulletinName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string AssociatedMailGroup { get; init; } = string.Empty;
    public string TriggerEvent { get; init; } = string.Empty;
    public string? MessageTemplate { get; init; }
    public bool IsActive { get; init; }
    public int FireCount { get; init; }
    public DateTime? LastFiredDateTime { get; init; }
    public DateTime CreatedDate { get; init; }
}
