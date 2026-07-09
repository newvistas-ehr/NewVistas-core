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
    /// <summary>Fallback institution when the user's home facility can't be resolved.</summary>
    private const string DefaultInstitutionId = "500";
    private const string DefaultInstitutionName = "NEW VISTAS MEDICAL CENTER";

    private HashSet<string> _keys = [];
    private HashSet<MenuArea> _accessibleAreas = [MenuArea.General];
    private HashSet<string> _features = [];

    /// <summary>The user's security keys, fetched once from the server.</summary>
    public IReadOnlySet<string> SecurityKeys => _keys;

    /// <summary>Whether the context has been initialized (keys loaded).</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// The user's home facility (File #200 field 13) resolved to a canonical institution id
    /// via the INSTITUTION-INDEX alias map — null when the profile has none or resolution failed.
    /// </summary>
    public string? HomeInstitutionId { get; private set; }

    /// <summary>Display name of the home institution, when resolved.</summary>
    public string? HomeInstitutionName { get; private set; }

    /// <summary>
    /// The institution the user is currently WORKING AS on multi-facility pages (bed board,
    /// transfer center, ADT). Defaults to the home institution (or the flagship "500");
    /// pages set it from their institution picker so the choice follows the circuit.
    /// </summary>
    public string ActingInstitutionId { get; set; } = DefaultInstitutionId;

    /// <summary>Display name matching <see cref="ActingInstitutionId"/>.</summary>
    public string ActingInstitutionName { get; set; } = DefaultInstitutionName;

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
            return;
        }

        // Site feature flags (ONCOLOGY, EXTERNAL_PHARMACY, …) — cached so the nav can gate
        // Modern/RPMS sections with a sync check, like the security keys. Best-effort: a
        // failure here must not undo the keys above, so it's a separate try (a flag-gated
        // section just won't show).
        try
        {
            ISiteParametersGrain site = grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            _features = [.. (await site.GetParametersAsync()).Features];
        }
        catch { /* leave _features empty */ }

        // Institution context — the user's home facility (NEW PERSON File #200 field 13,
        // legacy spellings like "INST-500") resolved to a canonical institution id via the
        // INSTITUTION-INDEX. Best-effort: multi-facility pages fall back to the flagship
        // ("500") when the profile has no institution or resolution fails.
        try
        {
            INewPersonGrain person = grains.GetGrain<INewPersonGrain>($"USER:{userId}");
            string? rawInstitutionId = (await person.GetPersonAsync()).InstitutionId;
            if (!string.IsNullOrWhiteSpace(rawInstitutionId))
            {
                IInstitutionIndexGrain index = grains.GetGrain<IInstitutionIndexGrain>("INSTITUTION-INDEX");
                string? resolved = await index.ResolveLegacyFacilityIdAsync(rawInstitutionId);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    string name = (await grains.GetGrain<IInstitutionGrain>($"INST:{resolved}").GetAsync()).Name;
                    HomeInstitutionId = resolved;
                    HomeInstitutionName = string.IsNullOrWhiteSpace(name) ? resolved : name;
                    ActingInstitutionId = HomeInstitutionId;
                    ActingInstitutionName = HomeInstitutionName;
                }
            }
        }
        catch { /* leave the "500" defaults */ }
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
    /// Check if a site feature flag is enabled for this site (e.g. <c>SiteFeatures.Oncology</c>).
    /// O(1) lookup against the site's cached <c>Features</c> set. Lets the role-scoped nav and
    /// pages gate Modern/RPMS areas without an async grain call on every render.
    /// </summary>
    public bool IsFeatureEnabled(string flag) => _features.Contains(flag);

    /// <summary>
    /// Clear cached state on logout.
    /// </summary>
    public void Clear()
    {
        _keys = [];
        _accessibleAreas = [MenuArea.General];
        _features = [];
        IsInitialized = false;
        HomeInstitutionId = null;
        HomeInstitutionName = null;
        ActingInstitutionId = DefaultInstitutionId;
        ActingInstitutionName = DefaultInstitutionName;
    }
}
