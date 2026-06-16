// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── AR account index entry ────────────────────────────────────────────────────

/// <summary>Lightweight summary of an AR account used in per-patient index lookups.</summary>
[GenerateSerializer]
public record ARAccountIndexEntry
{
    /// <summary>Unique AR account identifier.</summary>
    [Id(0)] public string ARAccountId { get; init; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Category of the AR charge (enum name string).</summary>
    [Id(2)] public string ARCategory { get; init; } = string.Empty;

    /// <summary>Current lifecycle status (enum name string).</summary>
    [Id(3)] public string ARStatus { get; init; } = string.Empty;

    /// <summary>Original charge amount when the account was established.</summary>
    [Id(4)] public decimal OriginalAmount { get; init; }

    /// <summary>Outstanding balance as of last update.</summary>
    [Id(5)] public decimal CurrentBalance { get; init; }

    /// <summary>Date the AR account was established.</summary>
    [Id(6)] public DateTime DateEstablished { get; init; }
}

// ─── AR account index state — per-patient ─────────────────────────────────────

/// <summary>
/// Per-patient index of all AR account summaries.
/// Derived from individual ARAccountGrain states; keyed as "AR-ACCT-IDX:{patientId}".
/// </summary>
[GenerateSerializer]
public class ARAccountIndexState
{
    /// <summary>Patient identifier this index belongs to.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All AR account summaries for this patient.</summary>
    [Id(1)] public List<ARAccountIndexEntry> Entries { get; set; } = new();
}
