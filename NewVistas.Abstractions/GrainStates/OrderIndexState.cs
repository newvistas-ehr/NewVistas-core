// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the per-patient order index grain.
/// Stores order grain keys sorted by StartDate descending (most recent first).
/// Unlike vitals (write-once), orders change status, so entries support update.
/// </summary>
[GenerateSerializer]
public class PatientOrderIndexState
{
    /// <summary>
    /// Patient ID this index belongs to
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// All order index entries, sorted by StartDate descending (most recent first).
    /// </summary>
    [Id(1)]
    public List<OrderIndexEntry> Entries { get; set; } = new();

    [Id(2)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight index entry for an order.
/// Stores enough metadata to support filtering by status and date range
/// without activating the order grain.
/// </summary>
[GenerateSerializer]
public class OrderIndexEntry
{
    /// <summary>
    /// The grain key of the order grain (ORDER-{Guid}).
    /// </summary>
    [Id(0)]
    public string OrderGrainKey { get; set; } = string.Empty;

    /// <summary>
    /// When the order was placed — for range queries.
    /// </summary>
    [Id(1)]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Order type — Lab, Pharmacy, Radiology, Consult, etc.
    /// </summary>
    [Id(2)]
    public string OrderType { get; set; } = string.Empty;

    /// <summary>
    /// Current status — Pending, Active, Hold, Discontinued, Completed, Expired.
    /// Updated when the order status changes via the workflow grain.
    /// </summary>
    [Id(3)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The orderable item text — for display without grain activation.
    /// </summary>
    [Id(4)]
    public string OrderText { get; set; } = string.Empty;

    /// <summary>
    /// Ordering provider name — for display without grain activation.
    /// </summary>
    [Id(5)]
    public string? ProviderName { get; set; }

    /// <summary>
    /// Whether the order has been electronically signed.
    /// Supports ORWORR filter code 11 (unsigned) without grain activation.
    /// </summary>
    [Id(6)]
    public bool IsSigned { get; set; }
}
