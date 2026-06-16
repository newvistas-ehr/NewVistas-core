// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Access Control Grain — per-user security keys and session management.
///
/// This is the authorization enforcement point for the entire Orleans cluster.
/// Every grain method call passes through the AuthorizationCallFilter, which reads
/// the caller's UserId from RequestContext and queries this grain to check security keys.
///
/// Maps to VistA:
///   File #200 field 51 "KEYS" multiple → File #19.1 SECURITY KEY
///   File #3.081 Sign-on Log — session tracking
///   MUMPS: $$HASKEY^XUSRB(DUZ,keyname), SETUP^XUSRB (session init)
///
/// Three-layer security model:
///   Layer 1: Authentication → ASP.NET Core Identity (who you are)
///   Layer 2: Authorization  → THIS GRAIN (what you can do — security keys)
///   Layer 3: Patient access → IPatientAccessControlGrain (can you see this patient)
///
/// Grain Key: "ACL:{userId}" where userId is the ASP.NET Core Identity user ID.
/// </summary>
public interface IAccessControlGrain : IGrainWithStringKey
{
    // ─── Security Keys (File #200 field 51 / File #19.1) ────────────────

    /// <summary>
    /// Check whether the user holds a specific security key.
    /// This is the hot path — called by AuthorizationCallFilter on every grain invocation.
    /// Mirrors MUMPS: $$HASKEY^XUSRB(DUZ,"ORES").
    /// </summary>
    Task<bool> HasKeyAsync(string keyName);

    /// <summary>
    /// Check whether the user holds ANY of the specified keys (OR logic).
    /// Used when a grain method accepts multiple keys (e.g., ORES or ORELSE).
    /// </summary>
    Task<bool> HasAnyKeyAsync(IEnumerable<string> keyNames);

    /// <summary>
    /// Check whether the user holds ALL of the specified keys (AND logic).
    /// Used when a grain method requires a combination of keys.
    /// </summary>
    Task<bool> HasAllKeysAsync(IEnumerable<string> keyNames);

    /// <summary>
    /// Get all security keys currently held by this user.
    /// Used for UI display (user profile, admin key management screen).
    /// </summary>
    Task<IReadOnlySet<string>> GetKeysAsync();

    /// <summary>
    /// Grant a security key to this user. Idempotent — no error if already held.
    /// Records an audit entry with who granted the key and why.
    /// Only callable by users holding XUMGR (system manager) key.
    /// </summary>
    Task GrantKeyAsync(string keyName, string grantedByUserId, string grantedByName, string? reason = null);

    /// <summary>
    /// Revoke a security key from this user. Idempotent — no error if not held.
    /// Records an audit entry with who revoked the key and why.
    /// Only callable by users holding XUMGR (system manager) key.
    /// </summary>
    Task RevokeKeyAsync(string keyName, string revokedByUserId, string revokedByName, string? reason = null);

    /// <summary>
    /// Bulk-set security keys for this user, replacing all existing keys.
    /// Used during initial user provisioning or role-based key assignment.
    /// Records audit entries for each key change.
    /// </summary>
    Task SetKeysAsync(List<string> keys, string setByUserId, string setByName, string? reason = null);

    /// <summary>
    /// Get the security key audit log (grant/revoke history).
    /// </summary>
    Task<List<GrainStates.SecurityKeyAuditEntry>> GetKeyAuditLogAsync();

    // ─── Session Management (Sign-on Log, File #3.081) ──────────────────

    /// <summary>
    /// Establish a new session for this user. Called after successful authentication.
    /// Sets HasActiveSession=true, records sign-on time and client info.
    /// Mirrors MUMPS: SETUP^XUSRB (post-login session initialization).
    /// </summary>
    Task StartSessionAsync(string? divisionId, string? divisionName, string? clientDevice, string? clientIpAddress);

    /// <summary>
    /// End the current session (explicit logout or timeout).
    /// Sets HasActiveSession=false. The AuthorizationCallFilter rejects calls
    /// from users without an active session.
    /// </summary>
    Task EndSessionAsync();

    /// <summary>
    /// Record activity to keep the session alive (heartbeat).
    /// Updates LastActivityTime. Called periodically by clients.
    /// </summary>
    Task TouchSessionAsync();

    /// <summary>
    /// Check whether the user has an active, non-expired session.
    /// Returns false if no session, or if LastActivityTime + TimeoutMinutes &lt; now.
    /// </summary>
    Task<bool> IsSessionActiveAsync();

    /// <summary>
    /// Get the full access control state (keys + session info).
    /// Used by admin screens and the AuthorizationCallFilter for bulk reads.
    /// </summary>
    Task<GrainStates.AccessControlState> GetAccessControlStateAsync();

    /// <summary>
    /// Set the session timeout in minutes. Default is 15 (VistA standard).
    /// Configurable per-user for accessibility or security requirements.
    /// </summary>
    Task SetSessionTimeoutAsync(int timeoutMinutes);
}
