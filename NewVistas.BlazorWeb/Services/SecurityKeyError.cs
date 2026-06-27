// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.BlazorWeb.Services;

/// <summary>
/// Turns a permission-denied exception from the silo's <c>AuthorizationCallFilter</c> into a
/// clinician-friendly message that names the required security key — instead of leaking the
/// raw "Access denied: user … requires any of [KEY] to call IGrain.MethodAsync" text.
///
/// The filter throws <see cref="UnauthorizedAccessException"/> with a message of the form
/// <c>"Access denied: user &lt;id&gt; requires any of [KEY1, KEY2] to call IFoo.BarAsync"</c>.
/// Use in a page's catch block:
/// <code>
/// catch (UnauthorizedAccessException ex) { error = SecurityKeyError.Describe(ex); }
/// catch (Exception ex) { error = $"Error: {ex.Message}"; }
/// </code>
/// <see cref="Describe"/> also accepts non-permission exceptions and returns the normal
/// <c>"Error: …"</c> text for them, so a single generic catch can delegate to it too.
/// </summary>
public static class SecurityKeyError
{
    /// <summary>True when the exception is a security-key permission denial.</summary>
    public static bool IsPermissionDenied(Exception ex) => ex is UnauthorizedAccessException;

    /// <summary>
    /// A friendly message for a permission denial (naming the required key and pointing at
    /// Security Key Management); the ordinary <c>"Error: …"</c> text for anything else.
    /// </summary>
    public static string Describe(Exception ex)
    {
        if (ex is not UnauthorizedAccessException) return $"Error: {ex.Message}";

        string? keys = ExtractKeys(ex.Message);
        return keys is null
            ? "You don't have permission for this action. Ask your site administrator to grant the required security key (Security Key Management)."
            : $"You don't have permission for this action — it needs the “{keys}” security key. " +
              "Ask your site administrator to grant it (Security Key Management).";
    }

    /// <summary>Pull the "[KEY1, KEY2]" list out of the filter's message, if present.</summary>
    private static string? ExtractKeys(string message)
    {
        int open = message.IndexOf('[');
        int close = open >= 0 ? message.IndexOf(']', open) : -1;
        if (open < 0 || close <= open) return null;
        string inner = message.Substring(open + 1, close - open - 1).Trim();
        return inner.Length > 0 ? inner : null;
    }
}
