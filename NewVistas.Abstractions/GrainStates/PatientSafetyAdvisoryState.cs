// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Delivery state of a single patient advisory receipt.</summary>
public enum AdvisoryReceiptStatus
{
    /// <summary>Recorded as sent by the provider.</summary>
    Sent = 0,

    /// <summary>Confirmed delivered by the channel (e.g., portal inbox).</summary>
    Delivered = 1,

    /// <summary>Patient (or staff on their behalf) acknowledged receipt.</summary>
    Acknowledged = 2,

    /// <summary>Delivery failed.</summary>
    Failed = 3,
}

/// <summary>
/// One record of an advisory a patient received: the exact message text that was
/// sent, by whom, through which channel, and when. This is the audit answer to
/// "what warning did this patient receive, and what did it say".
/// </summary>
[GenerateSerializer]
public class PatientAdvisoryReceipt
{
    /// <summary>Unique receipt id.</summary>
    [Id(0)]
    public string ReceiptId { get; set; } = string.Empty;

    /// <summary>The advisory this receipt is for.</summary>
    [Id(1)]
    public string AdvisoryId { get; set; } = string.Empty;

    /// <summary>Denormalized advisory title at time of send.</summary>
    [Id(2)]
    public string AdvisoryTitle { get; set; } = string.Empty;

    /// <summary>
    /// The exact message the patient received — the provider's edited text, not the
    /// advisory default. Captured verbatim so the record reflects what was actually said.
    /// </summary>
    [Id(3)]
    public string MessageSent { get; set; } = string.Empty;

    /// <summary>Provider who sent it.</summary>
    [Id(4)]
    public string SentByProviderId { get; set; } = string.Empty;

    /// <summary>Denormalized provider name.</summary>
    [Id(5)]
    public string SentByProviderName { get; set; } = string.Empty;

    /// <summary>Delivery channel.</summary>
    [Id(6)]
    public AdvisoryChannel Channel { get; set; } = AdvisoryChannel.PatientPortal;

    /// <summary>When the advisory was sent.</summary>
    [Id(7)]
    public DateTime SentDate { get; set; } = DateTime.UtcNow;

    /// <summary>Delivery/acknowledgement state.</summary>
    [Id(8)]
    public AdvisoryReceiptStatus Status { get; set; } = AdvisoryReceiptStatus.Sent;

    /// <summary>When the patient acknowledged, if applicable.</summary>
    [Id(9)]
    public DateTime? AcknowledgedDate { get; set; }
}

/// <summary>
/// Per-patient log of safety advisories received (VistA File #50-adjacent; analogous
/// to a patient bulletin/notification history). Keyed by patient id, this is the
/// source of truth for "what did this patient receive".
/// </summary>
[GenerateSerializer]
public class PatientSafetyAdvisoryState
{
    /// <summary>Patient id (the grain key).</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>All advisory receipts for this patient, newest appended last.</summary>
    [Id(1)]
    public List<PatientAdvisoryReceipt> Receipts { get; set; } = new();
}
