// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee Authorization Index Entry ────────────────────────────────────────────

/// <summary>Lightweight summary of a fee basis authorization for index lookup.</summary>
[GenerateSerializer]
public record FeeAuthorizationIndexEntry
{
    /// <summary>Unique identifier for the authorization.</summary>
    [Id(0)] public string AuthorizationId { get; init; } = string.Empty;

    /// <summary>Patient for whom the authorization was issued.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Display name of the vendor authorized to deliver services.</summary>
    [Id(2)] public string VendorName { get; init; } = string.Empty;

    /// <summary>Type of service authorized (e.g., "Outpatient", "Dental").</summary>
    [Id(3)] public string ServiceType { get; init; } = string.Empty;

    /// <summary>Lifecycle status of the authorization (e.g., "Active", "Exhausted").</summary>
    [Id(4)] public string Status { get; init; } = string.Empty;

    /// <summary>Maximum dollar amount approved under this authorization.</summary>
    [Id(5)] public decimal AuthorizedAmount { get; init; }

    /// <summary>Total amount spent from this authorization so far.</summary>
    [Id(6)] public decimal SpentAmount { get; init; }

    /// <summary>Date the authorization was issued.</summary>
    [Id(7)] public DateTime AuthorizationDate { get; init; }
}

// ─── Fee Authorization Index State ────────────────────────────────────────────

/// <summary>
/// Per-patient index of all fee basis authorizations.
/// Grain key: "FEE-AUTH-IDX:{patientId}".
/// </summary>
[GenerateSerializer]
public class FeeAuthorizationIndexState
{
    /// <summary>Patient whose authorizations are indexed here.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All authorization summaries for this patient.</summary>
    [Id(1)] public List<FeeAuthorizationIndexEntry> Entries { get; set; } = new();
}
