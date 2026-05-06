// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.Security;

/// <summary>
/// Well-known keys for Orleans RequestContext — set by clients at login time,
/// read by the AuthorizationCallFilter on every grain call.
///
/// Usage (client-side, after authentication):
///   RequestContext.Set(RequestContextKeys.UserId, "user-abc-123");
///   RequestContext.Set(RequestContextKeys.UserName, "SMITH,JOHN A");
///
/// These values propagate through the entire grain call chain automatically
/// (Orleans RequestContext flows across async grain-to-grain calls).
///
/// VistA equivalent: DUZ (user ID) and DUZ(0) (division) set in the MUMPS partition
/// after sign-on by SETUP^XUSRB.
/// </summary>
public static class RequestContextKeys
{
    /// <summary>
    /// The authenticated user's ID (ASP.NET Core Identity user ID / DUZ equivalent).
    /// Required on all grain calls. The filter rejects calls without this.
    /// </summary>
    public const string UserId = "UserId";

    /// <summary>
    /// The authenticated user's display name (for audit logging, not authorization).
    /// </summary>
    public const string UserName = "UserName";

    /// <summary>
    /// The division the user is logged into (for multi-division sites).
    /// Optional — used by grains that need division context.
    /// </summary>
    public const string DivisionId = "DivisionId";
}
