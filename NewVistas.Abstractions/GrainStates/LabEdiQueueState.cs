// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lab EDI Queue grain. Manages the outbound order queue
/// and inbound result tracking for a single reference laboratory.
/// </summary>
[GenerateSerializer]
public class LabEdiQueueState
{
    /// <summary>
    /// Reference lab identifier this queue belongs to
    /// </summary>
    [Id(0)]
    public string ReferenceLabId { get; set; } = string.Empty;

    /// <summary>
    /// Pending outbound orders awaiting transmission
    /// </summary>
    [Id(1)]
    public List<LabEdiQueueEntry> PendingOrders { get; set; } = new();

    /// <summary>
    /// Recent inbound results (kept to last 200)
    /// </summary>
    [Id(2)]
    public List<LabEdiQueueEntry> RecentResults { get; set; } = new();

    /// <summary>
    /// Total orders sent (lifetime counter)
    /// </summary>
    [Id(3)]
    public long TotalOrdersSent { get; set; }

    /// <summary>
    /// Total results received (lifetime counter)
    /// </summary>
    [Id(4)]
    public long TotalResultsReceived { get; set; }

    /// <summary>
    /// Timestamp of last queue activity
    /// </summary>
    [Id(5)]
    public DateTime? LastActivityDate { get; set; }
}

/// <summary>
/// An entry in the EDI order/result queue.
/// </summary>
[GenerateSerializer]
public class LabEdiQueueEntry
{
    /// <summary>
    /// Unique queue entry identifier
    /// </summary>
    [Id(0)]
    public string EntryId { get; set; } = string.Empty;

    /// <summary>
    /// Associated order identifier
    /// </summary>
    [Id(1)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier
    /// </summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient display name
    /// </summary>
    [Id(3)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Direction: OUTBOUND (order to ref lab) or INBOUND (result from ref lab)
    /// </summary>
    [Id(4)]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Current entry status (PENDING, SENT, RECEIVED, ERROR)
    /// </summary>
    [Id(5)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Date/time entry was queued
    /// </summary>
    [Id(6)]
    public DateTime QueuedDate { get; set; }

    /// <summary>
    /// Date/time entry was processed (sent or received)
    /// </summary>
    [Id(7)]
    public DateTime? ProcessedDate { get; set; }
}

/// <summary>
/// Aggregate queue status for dashboard display.
/// </summary>
[GenerateSerializer]
public class LabEdiQueueStatus
{
    /// <summary>
    /// Number of outbound orders pending transmission
    /// </summary>
    [Id(0)]
    public int PendingOutbound { get; set; }

    /// <summary>
    /// Number of inbound results pending review
    /// </summary>
    [Id(1)]
    public int PendingInbound { get; set; }

    /// <summary>
    /// Number of orders sent today
    /// </summary>
    [Id(2)]
    public int SentToday { get; set; }

    /// <summary>
    /// Number of results received today
    /// </summary>
    [Id(3)]
    public int ReceivedToday { get; set; }

    /// <summary>
    /// Date/time of last queue activity
    /// </summary>
    [Id(4)]
    public DateTime? LastActivityDate { get; set; }
}
