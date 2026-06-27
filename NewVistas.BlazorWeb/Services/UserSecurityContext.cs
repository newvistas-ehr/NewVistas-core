// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;

namespace NewVistas.BlazorWeb.Services;

/// <summary>
/// Scoped service that caches the logged-in user's security keys and precomputed
/// menu area access for the lifetime of the Blazor circuit (session).
///
/// Populated once after login. All subsequent checks are in-memory HashSet lookups.
/// </summary>
public sealed class UserSecurityContext
{
    private HashSet<string> _keys = [];
    private HashSet<MenuArea> _accessibleAreas = [MenuArea.General];

    /// <summary>The user's security keys, fetched once from the server.</summary>
    public IReadOnlySet<string> SecurityKeys => _keys;

    /// <summary>Whether the context has been initialized (keys loaded).</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Load the user's security keys directly from the silo's AccessControl grain and
    /// precompute accessible menu areas. Internal UIs talk to grains, not the WebServer —
    /// the WebServer is only for authentication and external callers. The grain call is
    /// authorized by the RequestContext that <see cref="OrleansGrainService"/> sets from
    /// the user's JWT, so a user reads their own keys.
    /// </summary>
    public async Task InitializeAsync(OrleansGrainService grains, string userId)
    {
        try
        {
            IAccessControlGrain acl = grains.GetGrain<IAccessControlGrain>($"ACL:{userId}");
            IReadOnlySet<string> keys = await acl.GetKeysAsync();

            _keys = [.. keys];
            _accessibleAreas = MenuAccessMap.GetAccessibleAreas(_keys);
            IsInitialized = true;   // only mark initialized once keys actually load
        }
        catch
        {
            // Leave IsInitialized=false on failure so the menu can retry (e.g., from
            // MainLayout) rather than silently collapsing to General-only for the session.
        }
    }

    /// <summary>
    /// Check if the user has access to a menu area. O(1) HashSet lookup.
    /// </summary>
    public bool HasAccess(MenuArea area) => _accessibleAreas.Contains(area);

    /// <summary>
    /// Check if the user holds a specific security key (e.g. <c>YS MH INSTRUMENT</c>).
    /// O(1) HashSet lookup. Lets pages gate a fine-grained, key-protected feature the
    /// coarse <see cref="MenuArea"/> map can't express. Returns false until the context is
    /// initialized — callers that must distinguish "no key" from "not loaded yet" should
    /// also check <see cref="IsInitialized"/>.
    /// </summary>
    public bool HasKey(string key) => _keys.Contains(key);

    /// <summary>
    /// Clear cached state on logout.
    /// </summary>
    public void Clear()
    {
        _keys = [];
        _accessibleAreas = [MenuArea.General];
        IsInitialized = false;
    }
}
