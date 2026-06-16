// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a MailMan mail group (distribution list).
/// VistA MAIL GROUP file (#3.8). MUMPS references: XMXGRP.m, XMG.m
/// </summary>
[GenerateSerializer]
public class MailGroupState
{
    /// <summary>
    /// Mail group name (.01)
    /// </summary>
    [Id(0)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the mail group (1)
    /// </summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Creator/owner user ID (2)
    /// </summary>
    [Id(2)]
    public string OrganizerId { get; set; } = string.Empty;

    /// <summary>
    /// Creator/owner display name
    /// </summary>
    [Id(3)]
    public string OrganizerName { get; set; } = string.Empty;

    /// <summary>
    /// Group members
    /// </summary>
    [Id(4)]
    public List<MailGroupMember> Members { get; set; } = new();

    /// <summary>
    /// Membership type: OPEN, CLOSED, RESTRICTED
    /// </summary>
    [Id(5)]
    public string MembershipType { get; set; } = "OPEN";

    /// <summary>
    /// Whether the group is active
    /// </summary>
    [Id(6)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date/time the group was created
    /// </summary>
    [Id(7)]
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date/time the group was last modified
    /// </summary>
    [Id(8)]
    public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Represents a member of a mail group.
/// </summary>
[GenerateSerializer]
public class MailGroupMember
{
    /// <summary>
    /// Member user identifier
    /// </summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Member display name
    /// </summary>
    [Id(1)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Member role: MEMBER, ORGANIZER, COORDINATOR
    /// </summary>
    [Id(2)]
    public string Role { get; set; } = "MEMBER";

    /// <summary>
    /// Date/time the member joined
    /// </summary>
    [Id(3)]
    public DateTime JoinedDate { get; set; }
}
