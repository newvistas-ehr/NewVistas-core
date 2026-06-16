// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Security;
using Orleans.Runtime;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Sets Orleans RequestContext to a SYSTEM identity with XUPROG (superuser) key
/// for demo data seeding operations. This allows demo/load endpoints to call
/// workflow grain methods that require security keys the current user may not hold.
///
/// VistA equivalent: POSTMASTER (DUZ=.5) running background tasks with
/// unrestricted access to FileMan.
///
/// Usage:
///   var saved = DemoSeedHelper.SetSystemContext();
///   try { /* call workflow methods */ }
///   finally { DemoSeedHelper.RestoreContext(saved); }
/// </summary>
public static class DemoSeedHelper
{
    /// <summary>
    /// The well-known user ID for the demo seed system account.
    /// Matches the ACL grain key "ACL:SYSTEM-SEED" created at startup.
    /// </summary>
    public const string SystemUserId = "SYSTEM-SEED";
    public const string SystemUserName = "SYSTEM,DEMO SEED";

    /// <summary>
    /// Captures the current RequestContext identity.
    /// </summary>
    public readonly record struct SavedContext(string? UserId, string? UserName, string? DivisionId);

    /// <summary>
    /// Sets RequestContext to the SYSTEM-SEED identity (holds XUPROG).
    /// Returns the previous context so it can be restored.
    /// </summary>
    public static SavedContext SetSystemContext()
    {
        var saved = new SavedContext(
            RequestContext.Get(RequestContextKeys.UserId) as string,
            RequestContext.Get(RequestContextKeys.UserName) as string,
            RequestContext.Get(RequestContextKeys.DivisionId) as string);

        RequestContext.Set(RequestContextKeys.UserId, SystemUserId);
        RequestContext.Set(RequestContextKeys.UserName, SystemUserName);

        return saved;
    }

    /// <summary>
    /// Restores the RequestContext to the previously saved identity.
    /// </summary>
    public static void RestoreContext(SavedContext saved)
    {
        if (saved.UserId is not null) RequestContext.Set(RequestContextKeys.UserId, saved.UserId);
        else RequestContext.Remove(RequestContextKeys.UserId);

        if (saved.UserName is not null) RequestContext.Set(RequestContextKeys.UserName, saved.UserName);
        else RequestContext.Remove(RequestContextKeys.UserName);

        if (saved.DivisionId is not null) RequestContext.Set(RequestContextKeys.DivisionId, saved.DivisionId);
        else RequestContext.Remove(RequestContextKeys.DivisionId);
    }
}
