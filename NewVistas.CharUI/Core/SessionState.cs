// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.CharUI.Core;

/// <summary>
/// User session state: facility, division, current user identity, security keys,
/// and session tracking.  Populated after successful login.
///
/// VistA equivalent: DUZ array set by SETUP^XUSRB after sign-on.
/// </summary>
public class SessionState
{
    // ── Facility / Division ────────────────────────────────────────────
    public string FacilityName { get; set; } = "NEWVISTAS MEDICAL CENTER";
    public string Division { get; set; } = "500";

    // ── User identity (populated at login) ─────────────────────────────
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    // ── Security keys held by the user (loaded after login) ────────────
    public HashSet<string> SecurityKeys { get; set; } = [];

    // ── Session management ─────────────────────────────────────────────
    public bool IsLoggedIn { get; set; }
    public DateTime? LoginTime { get; set; }
    public DateTime? LastActivity { get; set; }

    // ── Electronic signature hash (set after login via /api/auth) ──────
    public string? ElectronicSignatureHash { get; set; }

    /// <summary>Checks whether the user holds a specific security key.</summary>
    public bool HasKey(string keyName) => SecurityKeys.Contains(keyName);

    /// <summary>Checks whether the user holds any of the listed keys.</summary>
    public bool HasAnyKey(params string[] keyNames)
        => keyNames.Any(k => SecurityKeys.Contains(k));

    public void Clear()
    {
        UserId = string.Empty;
        UserName = string.Empty;
        SecurityKeys.Clear();
        IsLoggedIn = false;
        LoginTime = null;
        LastActivity = null;
        ElectronicSignatureHash = null;
    }
}
