// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a single immutable audit event.
/// Based on VistA AUDIT file (#1.1) — Kernel package XUSEC routines.
///
/// Domains map to VistA package namespaces:
///   ORDERS (OR), LABS (LR), PHARMACY (PS/PSO/PSI), NOTES (TIU),
///   VITALS (GMRV), ALLERGIES (GMRA), PROBLEMS (GMPL), CONSULTS (GMRC),
///   ADT (MAS), SCHEDULING (SD), PCE (PX), BCMA (PSB), IMAGING (MAG)
/// </summary>
[GenerateSerializer]
public class AuditEventState
{
    /// <summary>
    /// Unique event identifier — grain key (e.g., "AUDIT-{guid}").
    /// </summary>
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Patient ICN/ID this event relates to.
    /// Maps to VistA AUDIT file PATIENT field.
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Clinical domain: ORDERS, LABS, PHARMACY, NOTES, VITALS, ALLERGIES,
    /// PROBLEMS, CONSULTS, ADT, SCHEDULING, PCE, BCMA, IMAGING, DEMOGRAPHICS.
    /// Maps to VistA FILE NUMBER cross-reference in AUDIT.
    /// </summary>
    [Id(2)]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Action performed: VIEW, CREATE, UPDATE, DELETE, SIGN, COSIGN, VERIFY,
    /// DISCONTINUE, HOLD, RELEASE, CHECK-IN, CHECK-OUT, CANCEL, RETRACT.
    /// Maps to VistA ACTION TYPE field (.06).
    /// </summary>
    [Id(3)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity acted on (e.g., OrderState, TiuDocumentState, LabTestState).
    /// </summary>
    [Id(4)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Grain key of the entity acted on (e.g., "ORDER-{guid}").
    /// </summary>
    [Id(5)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// User/provider who performed the action.
    /// Maps to VistA NEW PERSON file (#200) pointer in AUDIT.
    /// </summary>
    [Id(6)]
    public string? UserId { get; set; }

    /// <summary>
    /// Display name of the user (e.g., "SMITH,JOHN").
    /// </summary>
    [Id(7)]
    public string? UserName { get; set; }

    /// <summary>
    /// Hospital location / division where the action occurred.
    /// Maps to VistA HOSPITAL LOCATION file (#44).
    /// </summary>
    [Id(8)]
    public string? LocationId { get; set; }

    [Id(9)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Human-readable description of what changed or what was accessed.
    /// </summary>
    [Id(10)]
    public string? Details { get; set; }

    /// <summary>
    /// Previous field value (for UPDATE actions). Maps to VistA OLD VALUE field.
    /// </summary>
    [Id(11)]
    public string? OldValue { get; set; }

    /// <summary>
    /// New field value (for UPDATE actions). Maps to VistA NEW VALUE field.
    /// </summary>
    [Id(12)]
    public string? NewValue { get; set; }

    /// <summary>
    /// UTC timestamp when the action occurred.
    /// Maps to VistA DATE/TIME RECORDED field (.01).
    /// </summary>
    [Id(13)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Id(14)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // ─── Tamper-Resistance (§170.315(d)(2)) ──────────────────────────

    /// <summary>
    /// SHA-256 hash of the previous audit event in the patient's chain.
    /// The first event in a patient's chain uses the genesis hash (SHA-256 of "GENESIS").
    /// Forms a hash chain that makes retroactive tampering detectable.
    /// </summary>
    [Id(15)]
    public string PreviousEventHash { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of this event's canonical content concatenated with PreviousEventHash.
    /// Computed once at write time; never changes. Any modification to the event data
    /// will cause this hash to not match a recomputation — proving tampering.
    /// </summary>
    [Id(16)]
    public string EventHash { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight summary stored in the patient audit index for fast listing.
/// Contains enough information to display the audit trail without loading each event grain.
/// </summary>
[GenerateSerializer]
public class AuditEventSummary
{
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    [Id(1)]
    public string Domain { get; set; } = string.Empty;

    [Id(2)]
    public string Action { get; set; } = string.Empty;

    [Id(3)]
    public string EntityType { get; set; } = string.Empty;

    [Id(4)]
    public string EntityId { get; set; } = string.Empty;

    [Id(5)]
    public string? UserName { get; set; }

    [Id(6)]
    public string? LocationName { get; set; }

    [Id(7)]
    public string? Details { get; set; }

    [Id(8)]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Hash of this event for tamper-resistance chain verification (§170.315(d)(2)).
    /// </summary>
    [Id(9)]
    public string EventHash { get; set; } = string.Empty;
}

/// <summary>
/// State for the per-patient audit index grain.
/// Holds ordered summaries for fast listing queries.
/// </summary>
[GenerateSerializer]
public class PatientAuditIndexState
{
    /// <summary>
    /// Chronological list of audit event summaries, newest appended last.
    /// Queries reverse this list for recency-first results.
    /// </summary>
    [Id(0)]
    public List<AuditEventSummary> Events { get; set; } = new();
}
