// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
    /// Load the user's security keys from the API and precompute accessible menu areas.
    /// Called once after successful login.
    /// </summary>
    public async Task InitializeAsync(HttpClient http, string userId)
    {
        try
        {
            var response = await http.GetFromJsonAsync<AclKeysResponse>(
                $"api/accesscontrol/users/{Uri.EscapeDataString(userId)}/keys");

            if (response?.Keys is not null)
            {
                _keys = [.. response.Keys];
                _accessibleAreas = MenuAccessMap.GetAccessibleAreas(_keys);
            }
        }
        catch
        {
            // If key fetch fails, default to General-only access.
            // The user can still log in but will only see general menu items.
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Check if the user has access to a menu area. O(1) HashSet lookup.
    /// </summary>
    public bool HasAccess(MenuArea area) => _accessibleAreas.Contains(area);

    /// <summary>
    /// Clear cached state on logout.
    /// </summary>
    public void Clear()
    {
        _keys = [];
        _accessibleAreas = [MenuArea.General];
        IsInitialized = false;
    }

    // DTO matching the API response shape
    private sealed class AclKeysResponse
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> Keys { get; set; } = [];
    }
}
