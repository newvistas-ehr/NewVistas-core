// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Mail Group Index Grain — singleton index of all mail groups.
/// Key: "MAILGRP-INDEX". Self-seeding with 6 demo mail groups.
/// VistA equivalent: "B" cross-reference on MAIL GROUP file (#3.8).
/// </summary>
public interface IMailGroupIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all mail groups. Seeds demo data on first call if empty.
    /// </summary>
    Task<List<GrainStates.MailGroupIndexEntry>> GetAllGroupsAsync();

    /// <summary>
    /// Adds or updates a mail group entry in the index.
    /// </summary>
    Task AddOrUpdateGroupAsync(GrainStates.MailGroupIndexEntry entry);

    /// <summary>
    /// Removes a mail group from the index.
    /// </summary>
    Task RemoveGroupAsync(string groupName);

    /// <summary>
    /// Searches mail groups by name or description (case-insensitive substring).
    /// </summary>
    Task<List<GrainStates.MailGroupIndexEntry>> SearchGroupsAsync(string searchTerm);
}
