// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Access Control grain — per-user security keys and session tracking.
///
/// Maps to VistA File #200 field 51 "KEYS" multiple (security keys held by a user)
/// and the Sign-on Log (File #3.081) for session tracking.
///
/// This grain is the single enforcement point for authorization across all Orleans clients
/// (Blazor in-process, WPF direct client, REST API). The AuthorizationCallFilter reads
/// the caller's UserId from RequestContext and queries this grain to validate access.
///
/// Grain key: "ACL:{userId}" where userId is the ASP.NET Core Identity user ID.
/// </summary>
[GenerateSerializer]
public class AccessControlState
{
    /// <summary>
    /// User ID — maps to ASP.NET Core Identity user ID (DUZ equivalent).
    /// </summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Security keys held by this user.
    /// Maps to VistA File #200 field 51 "KEYS" multiple → File #19.1.
    /// Examples: "ORES", "PROVIDER", "PSJ RPHARM", "LRLAB".
    /// Stored as a HashSet for O(1) lookup by the call filter.
    /// </summary>
    [Id(1)]
    public HashSet<string> SecurityKeys { get; set; } = new();

    /// <summary>
    /// Whether the user has an active session (currently logged in).
    /// Checked by the call filter — inactive sessions are rejected.
    /// </summary>
    [Id(2)]
    public bool HasActiveSession { get; set; }

    /// <summary>
    /// When the current session started (sign-on time).
    /// Maps to VistA Sign-on Log (File #3.081) field .01 DATE/TIME.
    /// </summary>
    [Id(3)]
    public DateTime? SessionStartTime { get; set; }

    /// <summary>
    /// Last activity timestamp for session timeout detection.
    /// Maps to VistA Sign-on Log field 1 "DATE/TIME LAST ACCESS".
    /// </summary>
    [Id(4)]
    public DateTime? LastActivityTime { get; set; }

    /// <summary>
    /// Client identifier (IP address or device name).
    /// Maps to VistA Sign-on Log field .02 "DEVICE".
    /// </summary>
    [Id(5)]
    public string? ClientDevice { get; set; }

    /// <summary>
    /// Client IP address.
    /// Maps to VistA Sign-on Log field 3 "IP ADDRESS".
    /// </summary>
    [Id(6)]
    public string? ClientIpAddress { get; set; }

    /// <summary>
    /// Division the user is logged into for this session.
    /// VistA users select a division at login when they have multi-division access.
    /// Maps to File #200 field 16 "DIVISION".
    /// </summary>
    [Id(7)]
    public string? SessionDivisionId { get; set; }

    /// <summary>
    /// Display name of the session division.
    /// </summary>
    [Id(8)]
    public string? SessionDivisionName { get; set; }

    /// <summary>
    /// Session timeout in minutes. Default 15 minutes (VistA standard).
    /// After this period of inactivity, the session is considered expired.
    /// </summary>
    [Id(9)]
    public int SessionTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Historical log of security key grants/revocations for audit trail.
    /// </summary>
    [Id(10)]
    public List<SecurityKeyAuditEntry> KeyAuditLog { get; set; } = new();

    /// <summary>
    /// Date this record was created.
    /// </summary>
    [Id(11)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date this record was last modified.
    /// </summary>
    [Id(12)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit entry for security key grant/revoke operations.
/// Tracks who granted or revoked a key and when.
/// </summary>
[GenerateSerializer]
public class SecurityKeyAuditEntry
{
    /// <summary>
    /// The security key that was granted or revoked.
    /// </summary>
    [Id(0)]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// "GRANT" or "REVOKE".
    /// </summary>
    [Id(1)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// User ID of the administrator who performed the action.
    /// </summary>
    [Id(2)]
    public string PerformedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the administrator.
    /// </summary>
    [Id(3)]
    public string PerformedByName { get; set; } = string.Empty;

    /// <summary>
    /// When the action occurred.
    /// </summary>
    [Id(4)]
    public DateTime ActionDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional reason/justification for the key change.
    /// </summary>
    [Id(5)]
    public string? Reason { get; set; }
}
