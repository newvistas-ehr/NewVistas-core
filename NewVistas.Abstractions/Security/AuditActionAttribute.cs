// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Security;

/// <summary>
/// Marks a grain interface method for automatic audit logging.
///
/// Applied to workflow grain methods alongside [RequiresSecurityKey].
/// The AuditCallFilter reads this attribute and, after the method executes
/// successfully, creates an immutable audit event grain recording the action.
///
/// ONC §170.315(d)(2) — auditable events and tamper-resistance.
/// ONC §170.315(d)(10) — auditing actions on health information.
///
/// Usage:
///   [AuditAction("ORDERS", "CREATE")]
///   [RequiresSecurityKey(SecurityKeys.ORES)]
///   Task&lt;string&gt; PlaceOrderAsync(...);
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuditActionAttribute : Attribute
{
    /// <summary>
    /// Clinical domain (e.g., "ORDERS", "LABS", "NOTES", "VITALS", "PROBLEMS").
    /// Maps to VistA AUDIT file #1.1 domain categories.
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// Action type (e.g., "CREATE", "UPDATE", "DELETE", "SIGN", "VERIFY", "DISCONTINUE").
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Optional entity type for the audit record (e.g., "ORDER", "PROBLEM", "NOTE").
    /// If not set, derived from Domain.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// When true, the <see cref="AuditCallFilter"/> skips this method entirely —
    /// the clinical-event-sourcing pipeline (per-patient
    /// <c>IPatientClinicalEventStreamGrain</c> with hash chain) is responsible
    /// for recording the change instead. Used for causal clinical-record writes
    /// where events are the legal source of truth.
    ///
    /// View/access auditing methods (<c>Action = "READ"</c>) and non-clinical
    /// auditable actions leave this as the default <c>false</c>.
    /// </summary>
    public bool IsClinicalWrite { get; set; }

    public AuditActionAttribute(string domain, string action)
    {
        Domain = domain;
        Action = action;
    }
}
