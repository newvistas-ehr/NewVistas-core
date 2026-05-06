// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Mail Group Grain Interface based on VistA MAIL GROUP file (#3.8).
/// Represents a MailMan distribution list. Key: "MAILGRP:{groupName}".
/// MUMPS references: XMXGRP.m, XMG.m
/// </summary>
public interface IMailGroupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete mail group state.
    /// </summary>
    Task<GrainStates.MailGroupState> GetGroupAsync();

    /// <summary>
    /// Creates a new mail group.
    /// </summary>
    Task CreateGroupAsync(
        string groupName,
        string description,
        string organizerId,
        string organizerName,
        string membershipType);

    /// <summary>
    /// Adds a member to the mail group.
    /// </summary>
    Task AddMemberAsync(string userId, string userName, string role);

    /// <summary>
    /// Removes a member from the mail group.
    /// </summary>
    Task RemoveMemberAsync(string userId);

    /// <summary>
    /// Updates the role of an existing member.
    /// </summary>
    Task UpdateMemberRoleAsync(string userId, string role);

    /// <summary>
    /// Deactivates the mail group.
    /// </summary>
    Task DeactivateAsync();

    /// <summary>
    /// Activates the mail group.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Gets all members of the mail group.
    /// </summary>
    Task<List<GrainStates.MailGroupMember>> GetMembersAsync();
}
