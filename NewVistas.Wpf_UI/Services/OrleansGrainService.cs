// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Wpf_UI.Services;

/// <summary>
/// Service that sets Orleans RequestContext from the user's JWT claims
/// before each grain call. ViewModels inject this instead of IGrainFactory
/// directly, ensuring the silo's AuthorizationCallFilter sees the caller's identity.
///
/// This is the WPF equivalent of OrleansRequestContextMiddleware in the WebServer
/// and OrleansGrainService in the BlazorWeb project.
///
/// VistA analogy: CPRS's RPC Broker sets DUZ in the MUMPS partition before each RPC.
/// </summary>
public class OrleansGrainService
{
    private readonly IGrainFactory _grainFactory;
    private readonly AuthService _authService;

    public OrleansGrainService(IGrainFactory grainFactory, AuthService authService)
    {
        _grainFactory = grainFactory;
        _authService = authService;
    }

    /// <summary>
    /// Get a grain reference with RequestContext pre-populated from the current user's JWT.
    /// </summary>
    public TGrainInterface GetGrain<TGrainInterface>(string grainKey) where TGrainInterface : IGrainWithStringKey
    {
        SetRequestContext();
        return _grainFactory.GetGrain<TGrainInterface>(grainKey);
    }

    /// <summary>
    /// The signed-in user's id, read from the JWT the WebServer issued. Null when not signed in.
    /// ViewModels use this to attribute writes (who granted a key, who signed a note) without
    /// needing their own token handling.
    /// </summary>
    public string? CurrentUserId => ClaimValue(
        ClaimTypes.NameIdentifier,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

    /// <summary>The signed-in user's display name, or null when not signed in.</summary>
    public string? CurrentUserName => ClaimValue(
        "display_name",
        ClaimTypes.Name,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

    private string? ClaimValue(params string[] claimTypes)
    {
        string? token = _authService?.Token;
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            foreach (string type in claimTypes)
            {
                string? v = jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }
        catch
        {
            // Unreadable token — treat as not signed in.
        }

        return null;
    }

    /// <summary>
    /// Set Orleans RequestContext from the JWT token stored in AuthService.
    /// Called automatically by GetGrain.
    /// </summary>
    public void SetRequestContext()
    {
        string? token = _authService?.Token;
        if (string.IsNullOrEmpty(token))
            return;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwt = handler.ReadJwtToken(token);

            string? userId = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier
                || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            string? displayName = jwt.Claims.FirstOrDefault(c => c.Type == "display_name")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name
                    || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                RequestContext.Set(RequestContextKeys.UserId, userId);

                // Touch the session to keep it alive — fire-and-forget
                _ = TouchSessionAsync(userId);
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                RequestContext.Set(RequestContextKeys.UserName, displayName);
            }
        }
        catch
        {
            // Token parsing failed — context will be empty, grain filter will reject
        }
    }

    private async Task TouchSessionAsync(string userId)
    {
        try
        {
            var aclGrain = _grainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");
            await aclGrain.TouchSessionAsync();
        }
        catch
        {
            // Non-critical — session touch failure shouldn't break the app
        }
    }
}
