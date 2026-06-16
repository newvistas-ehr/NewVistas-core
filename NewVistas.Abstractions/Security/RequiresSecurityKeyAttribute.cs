// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Security;

/// <summary>
/// Marks a grain interface method as requiring one or more VistA security keys.
///
/// Applied to grain interface methods (not implementations). The AuthorizationCallFilter
/// reads this attribute via reflection (cached) and checks the caller's keys from
/// RequestContext before allowing the grain method to execute.
///
/// Usage:
///   [RequiresSecurityKey(SecurityKeys.ORES)]
///   Task PlaceOrderAsync(...);
///
///   [RequiresSecurityKey(SecurityKeys.LRLAB, SecurityKeys.LRVERIFY, RequireAll = false)]
///   Task AccessLabAsync(...);  // user needs ANY of these keys
///
/// The XUPROG key always bypasses all checks (VistA programmer access).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiresSecurityKeyAttribute : Attribute
{
    /// <summary>
    /// The security key(s) required to call this method.
    /// </summary>
    public string[] Keys { get; }

    /// <summary>
    /// If true, the caller must hold ALL listed keys.
    /// If false (default), the caller needs ANY ONE of the listed keys.
    /// </summary>
    public bool RequireAll { get; set; } = false;

    /// <summary>
    /// Creates a new security key requirement.
    /// </summary>
    /// <param name="keys">One or more security key names from <see cref="SecurityKeys"/>.</param>
    public RequiresSecurityKeyAttribute(params string[] keys)
    {
        if (keys == null || keys.Length == 0)
            throw new ArgumentException("At least one security key must be specified.", nameof(keys));
        Keys = keys;
    }
}
