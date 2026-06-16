// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Claims;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// ASP.NET Core middleware that bridges HttpContext.User (JWT claims) to Orleans RequestContext.
///
/// Runs after UseAuthentication/UseAuthorization but before controllers execute.
/// For authenticated requests, sets:
///   - RequestContextKeys.UserId   → ClaimTypes.NameIdentifier (ASP.NET Core Identity user ID)
///   - RequestContextKeys.UserName → "display_name" claim (LAST,FIRST MI format)
///
/// This ensures the AuthorizationCallFilter in the Orleans silo can read the caller's
/// identity from RequestContext and enforce [RequiresSecurityKey] on grain methods.
///
/// VistA equivalent: setting DUZ and DUZ(0) in the MUMPS partition after sign-on,
/// which then flows into every RPC call.
/// </summary>
public class OrleansRequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public OrleansRequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, IGrainFactory grainFactory)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? displayName = httpContext.User.FindFirstValue("display_name")
                ?? httpContext.User.FindFirstValue(ClaimTypes.Name);

            if (!string.IsNullOrEmpty(userId))
            {
                RequestContext.Set(RequestContextKeys.UserId, userId);

                // Touch the session on each authenticated request — keeps the session
                // alive while the user is active. If no activity for SessionTimeoutMinutes,
                // IsSessionActiveAsync() returns false and the filter rejects calls.
                // This implements §170.315(d)(5) automatic access time-out.
                var aclGrain = grainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");
                await aclGrain.TouchSessionAsync();
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                RequestContext.Set(RequestContextKeys.UserName, displayName);
            }
        }

        try
        {
            await _next(httpContext);
        }
        finally
        {
            // Clean up RequestContext after the request completes to prevent
            // leaking identity across requests on the same thread.
            RequestContext.Remove(RequestContextKeys.UserId);
            RequestContext.Remove(RequestContextKeys.UserName);
            RequestContext.Remove(RequestContextKeys.DivisionId);
        }
    }
}

/// <summary>
/// Extension method for registering the middleware in the pipeline.
/// </summary>
public static class OrleansRequestContextMiddlewareExtensions
{
    public static IApplicationBuilder UseOrleansRequestContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<OrleansRequestContextMiddleware>();
    }
}
