// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight projection of a mail group for index/search purposes.
/// </summary>
[GenerateSerializer]
public class MailGroupIndexEntry
{
    /// <summary>
    /// Mail group name
    /// </summary>
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the mail group
    /// </summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Number of members in the group
    /// </summary>
    [Id(2)]
    public int MemberCount { get; set; }

    /// <summary>
    /// Whether the group is active
    /// </summary>
    [Id(3)]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Index grain state for all mail groups.
/// VistA equivalent: "B" cross-reference on MAIL GROUP file (#3.8).
/// </summary>
[GenerateSerializer]
public class MailGroupIndexState
{
    /// <summary>
    /// All registered mail groups
    /// </summary>
    [Id(0)]
    public List<MailGroupIndexEntry> Groups { get; set; } = new();
}
