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
