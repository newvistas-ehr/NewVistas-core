// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Patient Access Control state — VistA DG SENSITIVITY (.01) and DG SECURITY LOG File #38.1.
/// Tracks sensitive patient records, authorized providers (treating team), and break-the-glass audit trail.
/// </summary>
[GenerateSerializer]
public class PatientAccessControlState
{
    /// <summary>
    /// Patient ID this access control record belongs to.
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this patient record is marked as sensitive (DG SENSITIVITY .01).
    /// Sensitive records include HIV, substance abuse, behavioral health, employee records.
    /// </summary>
    [Id(1)]
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Sensitivity level — "STANDARD", "ELEVATED", "HIGH".
    /// Maps to DG SECURITY LOG File #38.1 access restriction levels.
    /// </summary>
    [Id(2)]
    public string SensitivityLevel { get; set; } = string.Empty;

    /// <summary>
    /// Categories of sensitivity — e.g. "HIV", "SICKLE_CELL", "SUBSTANCE_ABUSE", "BEHAVIORAL", "EMPLOYEE".
    /// </summary>
    [Id(3)]
    public List<string> SensitivityCategories { get; set; } = new();

    /// <summary>
    /// Audit trail of who accessed this patient record, when, and why (break-the-glass).
    /// </summary>
    [Id(4)]
    public List<PatientAccessLog> AccessLog { get; set; } = new();

    /// <summary>
    /// Providers with explicit access to this sensitive record (treating team).
    /// </summary>
    [Id(5)]
    public List<string> AuthorizedProviderIds { get; set; } = new();

    /// <summary>
    /// Date record last modified.
    /// </summary>
    [Id(6)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date record created.
    /// </summary>
    [Id(7)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the patient has given general written consent for disclosure of
    /// 42 CFR Part 2 substance use disorder / mental health records for TPO.
    /// </summary>
    [Id(8)]
    public bool HasPart2Consent { get; set; }

    /// <summary>
    /// Date the Part 2 consent was signed.
    /// </summary>
    [Id(9)]
    public DateTime? Part2ConsentDate { get; set; }

    /// <summary>
    /// Scope of the Part 2 consent (e.g., "TPO", "TREATMENT_ONLY", "FULL").
    /// </summary>
    [Id(10)]
    public string Part2ConsentScope { get; set; } = string.Empty;

    /// <summary>
    /// The patient's own sharing preference (ADR-002 Phase 4). Maximal openness is a FIRST-CLASS
    /// choice, not just the restrictive end of the dial: a patient who opts into
    /// <see cref="PatientSharePreference.OpenForTeachingAndResearch"/> is accessed frictionlessly (still
    /// audited) even if the record is otherwise flagged. The patient's choice — either direction — wins
    /// for their own record.
    /// </summary>
    [Id(11)]
    public PatientSharePreference SharePreference { get; set; } = PatientSharePreference.Default;

    /// <summary>
    /// Auto-established treatment relationships (ADR-002 Phase 4b) — the surgical case, an active order,
    /// an appointment, unit coverage, a consult. These grant frictionless access without a hand-curated
    /// list, so the surgical cast and floor coverage are authorized automatically. May carry an expiry.
    /// </summary>
    [Id(12)]
    public List<TreatmentRelationship> Relationships { get; set; } = new();
}

/// <summary>How a treatment relationship was established (ADR-002 Phase 4b).</summary>
public enum TreatmentRelationshipReason
{
    CareTeam = 0,
    Encounter = 1,
    Order = 2,
    Surgery = 3,
    Appointment = 4,
    UnitCoverage = 5,
    Consult = 6,
    Admission = 7
}

/// <summary>An auto-established treatment relationship granting a viewer frictionless access.</summary>
[GenerateSerializer]
public class TreatmentRelationship
{
    [Id(0)] public string UserId { get; set; } = string.Empty;
    [Id(1)] public TreatmentRelationshipReason Reason { get; set; }
    /// <summary>The source record (order id, surgery id, appointment id, unit id, …).</summary>
    [Id(2)] public string SourceRef { get; set; } = string.Empty;
    [Id(3)] public DateTime EstablishedDate { get; set; } = DateTime.UtcNow;
    /// <summary>Optional expiry — e.g. an OR-case relationship lapses after the documentation tail.</summary>
    [Id(4)] public DateTime? ExpiresDate { get; set; }
}

/// <summary>
/// A patient's own preference for how broadly their record is shared (ADR-002 Phase 4).
/// </summary>
public enum PatientSharePreference
{
    /// <summary>Normal — sensitivity + treatment-relationship rules apply.</summary>
    Default = 0,
    /// <summary>Patient wants the record treated as sensitive (still never gates the treating team).</summary>
    Restricted = 1,
    /// <summary>Patient has opted into open sharing for teaching/research — access allowed (still audited).</summary>
    OpenForTeachingAndResearch = 2
}

/// <summary>
/// The outcome of a patient-access decision (ADR-002 Phase 4). The treatment relationship is NEVER
/// gated; break-the-glass is only for access WITHOUT a relationship and never hard-blocks
/// (RequiresBreakTheGlass is a soft signal — attest to proceed).
/// </summary>
public enum PatientAccessOutcome
{
    /// <summary>Open record — no restriction.</summary>
    Allowed = 0,
    /// <summary>Sensitive, but the viewer has a treatment relationship — frictionless (heightened audit).</summary>
    AllowedByRelationship = 1,
    /// <summary>The patient opted into open sharing.</summary>
    AllowedByOpenConsent = 2,
    /// <summary>No relationship, but the viewer attested break-the-glass — proceed (audited + notifiable).</summary>
    AllowedByBreakTheGlass = 3,
    /// <summary>Sensitive + no relationship + not yet attested — SOFT: attest break-the-glass to proceed.</summary>
    RequiresBreakTheGlass = 4
}

/// <summary>The result of a patient-access decision. Every decision (including a pending-BTG one) is audited.</summary>
[GenerateSerializer]
public class PatientAccessDecision
{
    [Id(0)] public PatientAccessOutcome Outcome { get; set; }
    /// <summary>True unless the outcome is <see cref="PatientAccessOutcome.RequiresBreakTheGlass"/>.</summary>
    [Id(1)] public bool Granted { get; set; }
    [Id(2)] public bool WasBreakTheGlass { get; set; }
    /// <summary>Whether the record is sensitive (revealed to the viewer at the boundary — inherent to a BTG prompt).</summary>
    [Id(3)] public bool IsSensitive { get; set; }
    [Id(4)] public string Message { get; set; } = string.Empty;
}

/// <summary>
/// A single entry in the patient access audit log.
/// Records who accessed the patient record, when, and under what justification.
/// </summary>
[GenerateSerializer]
public class PatientAccessLog
{
    /// <summary>
    /// User ID of the person who accessed the record.
    /// </summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the user who accessed the record.
    /// </summary>
    [Id(1)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Date and time of the access event.
    /// </summary>
    [Id(2)]
    public DateTime AccessDateTime { get; set; }

    /// <summary>
    /// Reason for access — "TREATING_PROVIDER", "BREAK_THE_GLASS", "EMERGENCY", "ADMINISTRATIVE".
    /// </summary>
    [Id(3)]
    public string AccessReason { get; set; } = string.Empty;

    /// <summary>
    /// Whether this access was a break-the-glass override of sensitivity restrictions.
    /// </summary>
    [Id(4)]
    public bool WasBreakTheGlass { get; set; }

    /// <summary>
    /// Free-text justification for break-the-glass access.
    /// </summary>
    [Id(5)]
    public string? JustificationText { get; set; }
}
