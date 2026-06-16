// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Security;

/// <summary>
/// Cluster-wide authorization filter — runs before every grain method invocation.
///
/// Reads the caller's UserId from <see cref="RequestContext"/>, queries the
/// <see cref="IAccessControlGrain"/> for held security keys, and checks against
/// <see cref="RequiresSecurityKeyAttribute"/> on the target grain interface method.
///
/// VistA equivalent: the Kernel security layer that checks $$HASKEY^XUSRB(DUZ,key)
/// before executing any menu option or RPC.
///
/// Performance optimization:
///   Authorization is enforced at the workflow/API gateway grains (IPatientWorkflowGrain,
///   controllers), NOT on internal domain grains (IOrderGrain, IVitalGrain, etc.).
///   Domain grains are only reachable through workflow grains — they are never called
///   directly by UI or external clients. This mirrors VistA, where security keys are
///   checked at the menu option/RPC level, not inside individual FileMan routines.
///
///   The filter skips grain interfaces that have zero [RequiresSecurityKey] methods
///   (the "enforced interface" cache). This avoids any overhead on the thousands of
///   grain-to-grain calls that happen inside workflow orchestration.
///
/// Design:
///   - Enforced interface cache: O(1) check — does this interface have ANY secured methods?
///   - Method attribute cache: O(1) lookup — which keys does this specific method need?
///   - IAccessControlGrain is exempt (avoids circular grain calls).
///   - XUPROG holders bypass all key checks (VistA programmer access).
///   - Grain-to-grain calls propagate RequestContext automatically.
/// </summary>
public class AuthorizationCallFilter : IIncomingGrainCallFilter
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AuthorizationCallFilter> _logger;

    /// <summary>
    /// Fast-path cache: does this grain interface type have ANY methods with
    /// [RequiresSecurityKey]? If false, the entire grain is skipped — no per-method
    /// lookup, no RequestContext read, no ACL grain call.
    ///
    /// This is what makes internal domain grains (IOrderGrain, IVitalGrain, etc.)
    /// zero-cost. Only gateway grains (IPatientWorkflowGrain) have secured methods.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> _enforcedInterfaceCache = new();

    /// <summary>
    /// Per-method cache: maps (interface type, method name) → attribute (or null).
    /// Only consulted for interfaces that passed the enforced-interface check.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type InterfaceType, string MethodName), RequiresSecurityKeyAttribute?> _attributeCache = new();

    public AuthorizationCallFilter(IGrainFactory grainFactory, ILogger<AuthorizationCallFilter> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        // 1. Get the declaring interface type from the method's reflection info.
        Type? grainInterfaceType = context.InterfaceMethod.DeclaringType;
        if (grainInterfaceType == null)
        {
            await context.Invoke();
            return;
        }

        // 2. Fast-path: skip grain interfaces with zero secured methods.
        //    This covers all internal domain grains (IOrderGrain, IVitalGrain,
        //    ILabTestGrain, etc.) and the exempt grains (IAccessControlGrain,
        //    INewPersonGrain) in one O(1) dictionary lookup.
        if (!IsEnforcedInterface(grainInterfaceType))
        {
            await context.Invoke();
            return;
        }

        // 3. Look up [RequiresSecurityKey] on the specific method (cached)
        RequiresSecurityKeyAttribute? requirement = GetRequirement(context.InterfaceMethod);
        if (requirement == null)
        {
            // This method on an enforced interface has no attribute — unrestricted
            await context.Invoke();
            return;
        }

        // 4. Read caller identity from RequestContext
        string? userId = RequestContext.Get(RequestContextKeys.UserId) as string;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "Authorization denied: no UserId in RequestContext for {GrainType}.{Method}",
                grainInterfaceType.Name, context.InterfaceMethod.Name);
            throw new UnauthorizedAccessException(
                $"Access denied: no authenticated user. {grainInterfaceType.Name}.{context.InterfaceMethod.Name} requires security key(s): {string.Join(", ", requirement.Keys)}");
        }

        // 5. Query the user's AccessControlGrain
        IAccessControlGrain aclGrain = _grainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");

        // 5a. Session timeout check — §170.315(d)(5) automatic access time-out.
        //     If the user's session has expired (LastActivityTime + TimeoutMinutes < now),
        //     reject the call. VistA equivalent: auto-logoff after inactivity.
        bool sessionActive = await aclGrain.IsSessionActiveAsync();
        if (!sessionActive)
        {
            _logger.LogWarning(
                "Authorization denied: user {UserId} has no active session for {GrainType}.{Method}",
                userId, grainInterfaceType.Name, context.InterfaceMethod.Name);
            throw new UnauthorizedAccessException(
                $"Access denied: session expired or not started. User {userId} must re-authenticate.");
        }

        // 5b. XUPROG bypasses all key checks (VistA programmer/superuser access)
        if (await aclGrain.HasKeyAsync(SecurityKeys.XUPROG))
        {
            await context.Invoke();
            return;
        }

        // 5c. Check required keys based on RequireAll flag
        bool authorized = requirement.RequireAll
            ? await aclGrain.HasAllKeysAsync(requirement.Keys)
            : await aclGrain.HasAnyKeyAsync(requirement.Keys);

        if (!authorized)
        {
            string mode = requirement.RequireAll ? "all of" : "any of";
            _logger.LogWarning(
                "Authorization denied: user {UserId} lacks {Mode} [{Keys}] for {GrainType}.{Method}",
                userId, mode, string.Join(", ", requirement.Keys),
                grainInterfaceType.Name, context.InterfaceMethod.Name);
            throw new UnauthorizedAccessException(
                $"Access denied: user {userId} requires {mode} [{string.Join(", ", requirement.Keys)}] to call {grainInterfaceType.Name}.{context.InterfaceMethod.Name}");
        }

        // 6. Authorized — proceed with the grain call
        await context.Invoke();
    }

    /// <summary>
    /// Check whether this grain interface has ANY methods with [RequiresSecurityKey].
    /// Scans all methods on the interface (and its inherited interfaces) once, then caches.
    /// Returns false for domain grains, reference grains, and infrastructure grains.
    /// Returns true only for gateway grains like IPatientWorkflowGrain.
    /// </summary>
    private static bool IsEnforcedInterface(Type grainInterfaceType)
    {
        return _enforcedInterfaceCache.GetOrAdd(grainInterfaceType, static type =>
        {
            // Scan this interface's own methods
            foreach (MethodInfo method in type.GetMethods())
            {
                if (method.GetCustomAttribute<RequiresSecurityKeyAttribute>() != null)
                    return true;
            }

            // Scan inherited interfaces (e.g., if IPatientWorkflowGrain extends another)
            foreach (Type iface in type.GetInterfaces())
            {
                foreach (MethodInfo method in iface.GetMethods())
                {
                    if (method.GetCustomAttribute<RequiresSecurityKeyAttribute>() != null)
                        return true;
                }
            }

            return false;
        });
    }

    /// <summary>
    /// Get the [RequiresSecurityKey] attribute for a grain interface method, cached.
    /// Returns null if the method has no attribute (unrestricted).
    /// Only called for methods on enforced interfaces.
    /// </summary>
    private static RequiresSecurityKeyAttribute? GetRequirement(MethodInfo method)
    {
        Type declaringType = method.DeclaringType!;
        var key = (declaringType, method.Name);
        return _attributeCache.GetOrAdd(key, _ =>
            method.GetCustomAttribute<RequiresSecurityKeyAttribute>());
    }
}
