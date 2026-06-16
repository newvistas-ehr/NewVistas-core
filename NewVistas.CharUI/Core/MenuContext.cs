// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.CharUI.Core;

/// <summary>
/// Dependency bag passed to all menus: Orleans client, patient context, session state.
///
/// Every grain call flows through this class so that RequestContext identity
/// is set automatically — matching the WebServer's OrleansRequestContextMiddleware
/// behaviour (which bridges HttpContext.User → Orleans RequestContext).
///
/// The AuthorizationCallFilter reads these RequestContext values on every inbound
/// grain call and rejects any that are missing UserId.
/// </summary>
public class MenuContext
{
    public required IClusterClient Client { get; init; }
    public required PatientContext Patient { get; init; }
    public required SessionState Session { get; init; }

    /// <summary>
    /// Sets the Orleans RequestContext identity keys for the current session.
    /// Must be called once after login and before any grain calls.
    /// Values propagate through the entire grain call chain automatically.
    /// </summary>
    public void SetRequestContext()
    {
        RequestContext.Set(RequestContextKeys.UserId, Session.UserId);
        RequestContext.Set(RequestContextKeys.UserName, Session.UserName);
        RequestContext.Set(RequestContextKeys.DivisionId, Session.Division);
    }

    /// <summary>
    /// Gets the workflow grain for the currently selected patient.
    /// Ensures RequestContext is always set before the call.
    /// </summary>
    public IPatientWorkflowGrain GetWorkflow()
    {
        EnsureRequestContext();
        return Client.GetGrain<IPatientWorkflowGrain>(Patient.PatientId!);
    }

    /// <summary>
    /// Gets any grain by type and key (for system-level grains).
    /// Ensures RequestContext is always set before the call.
    /// </summary>
    public T GetGrain<T>(string key) where T : IGrainWithStringKey
    {
        EnsureRequestContext();
        return Client.GetGrain<T>(key);
    }

    /// <summary>
    /// Gets the AccessControl grain for the logged-in user.
    /// </summary>
    public IAccessControlGrain GetAccessControl()
    {
        EnsureRequestContext();
        return Client.GetGrain<IAccessControlGrain>($"ACL:{Session.UserId}");
    }

    /// <summary>
    /// Sends a session heartbeat (keeps the session alive, resetting the
    /// inactivity timeout per §170.315(d)(5)).
    /// </summary>
    public async Task TouchSessionAsync()
    {
        try
        {
            EnsureRequestContext();
            await GetAccessControl().TouchSessionAsync();
            Session.LastActivity = DateTime.UtcNow;
        }
        catch { /* swallow — heartbeat is best-effort */ }
    }

    private void EnsureRequestContext()
    {
        // Re-set on every call chain start — Orleans RequestContext is
        // async-local, so it's safe and cheap to refresh.
        if (Session.IsLoggedIn)
        {
            RequestContext.Set(RequestContextKeys.UserId, Session.UserId);
            RequestContext.Set(RequestContextKeys.UserName, Session.UserName);
            RequestContext.Set(RequestContextKeys.DivisionId, Session.Division);
        }
    }
}
