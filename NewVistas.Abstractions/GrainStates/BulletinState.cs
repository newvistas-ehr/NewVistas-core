// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a MailMan bulletin — an event-triggered automatic notification.
/// VistA BULLETIN file (#3.6). MUMPS references: XMBBULL.m, XMB.m
/// </summary>
[GenerateSerializer]
public class BulletinState
{
    /// <summary>
    /// Bulletin name (.01)
    /// </summary>
    [Id(0)]
    public string BulletinName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the bulletin (1)
    /// </summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Mail group that receives this bulletin (2)
    /// </summary>
    [Id(2)]
    public string AssociatedMailGroup { get; set; } = string.Empty;

    /// <summary>
    /// Whether the bulletin is active
    /// </summary>
    [Id(3)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Event that triggers this bulletin (e.g., NEW_ORDER, STAT_RESULT, ADT_ADMIT, CONSULT_REQUEST)
    /// </summary>
    [Id(4)]
    public string TriggerEvent { get; set; } = string.Empty;

    /// <summary>
    /// Template for auto-generated messages
    /// </summary>
    [Id(5)]
    public string? MessageTemplate { get; set; }

    /// <summary>
    /// Number of times this bulletin has fired
    /// </summary>
    [Id(6)]
    public int FireCount { get; set; }

    /// <summary>
    /// Date/time this bulletin last fired
    /// </summary>
    [Id(7)]
    public DateTime? LastFiredDateTime { get; set; }

    /// <summary>
    /// Date/time the bulletin was created
    /// </summary>
    [Id(8)]
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date/time the bulletin was last modified
    /// </summary>
    [Id(9)]
    public DateTime LastModifiedDate { get; set; }
}
