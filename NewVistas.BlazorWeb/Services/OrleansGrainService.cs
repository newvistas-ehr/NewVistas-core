// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;

namespace NewVistas.BlazorWeb.Services;

/// <summary>
/// Circuit-scoped service that sets Orleans RequestContext from the user's JWT
/// claims before each grain call. Blazor pages inject this instead of IGrainFactory
/// directly, ensuring the silo's AuthorizationCallFilter sees the caller's identity.
///
/// This is the Blazor equivalent of OrleansRequestContextMiddleware in the WebServer.
///
/// VistA analogy: CPRS's RPC Broker sets DUZ in the MUMPS partition before each RPC.
/// This service does the same — sets UserId/UserName in RequestContext before each
/// grain factory call, so the identity propagates through the entire grain call chain.
/// </summary>
public class OrleansGrainService
{
    private readonly IGrainFactory _grainFactory;
    private readonly JwtAuthenticationStateProvider _authProvider;

    public OrleansGrainService(IGrainFactory grainFactory, JwtAuthenticationStateProvider authProvider)
    {
        _grainFactory = grainFactory;
        _authProvider = authProvider;
    }

    /// <summary>
    /// The current user's id (NEW PERSON / login identity) parsed from the JWT,
    /// or null if not signed in. This is the key suffix for the provider's own
    /// data — e.g. the "PROV-PAT-IDX:{userId}" My Patients index.
    /// </summary>
    public string? CurrentUserId => ReadClaim(ClaimTypes.NameIdentifier,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

    /// <summary>The current user's display name parsed from the JWT, or null.</summary>
    public string? CurrentUserName => ReadClaim("display_name", ClaimTypes.Name,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

    private string? ReadClaim(params string[] claimTypes)
    {
        string? token = _authProvider.Token;
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            foreach (string type in claimTypes)
            {
                string? value = jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        catch
        {
            // Token parsing failed — treat as not signed in.
        }
        return null;
    }

    /// <summary>
    /// Get a grain reference with RequestContext pre-populated from the current user's JWT.
    /// The context is set on the async-local before returning the grain reference, so any
    /// subsequent awaited calls on the reference carry the identity through to the silo.
    /// </summary>
    public TGrainInterface GetGrain<TGrainInterface>(string grainKey) where TGrainInterface : IGrainWithStringKey
    {
        SetRequestContext();
        return _grainFactory.GetGrain<TGrainInterface>(grainKey);
    }

    /// <summary>
    /// Set Orleans RequestContext from the JWT token stored in the circuit.
    /// Called automatically by GetGrain, but can also be called explicitly before
    /// performing multiple grain operations in sequence.
    /// </summary>
    public void SetRequestContext()
    {
        string? token = _authProvider.Token;
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

                // Touch the session to keep it alive — mirrors the middleware behavior
                // in WebServer. Fire-and-forget to avoid blocking the UI thread.
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
            // Non-critical — session touch failure shouldn't break the UI
        }
    }
}
