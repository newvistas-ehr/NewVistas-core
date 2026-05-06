// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Access Control API — Security Key Management and Session Administration.
///
/// Manages user security keys (VistA File #200 field 51 / File #19.1).
/// Used by system administrators (XUMGR key holders) to provision user access.
///
/// Routes: api/accesscontrol/users/{userId}/keys/...
/// </summary>
[Authorize]
[ApiController]
[Route("api/accesscontrol")]
[Produces("application/json")]
public class AccessControlController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AccessControlController> _logger;

    public AccessControlController(IGrainFactory grainFactory, ILogger<AccessControlController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IAccessControlGrain GetAclGrain(string userId) =>
        _grainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private string GetCurrentUserName() =>
        User.FindFirstValue("display_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? "UNKNOWN";

    // ─── Key Management ─────────────────────────────────────────────────

    /// <summary>
    /// Get all security keys held by a user.
    /// </summary>
    [HttpGet("users/{userId}/keys")]
    [ProducesResponseType(typeof(AclKeysResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AclKeysResponse>> GetKeys(string userId)
    {
        try
        {
            IAccessControlGrain acl = GetAclGrain(userId);
            IReadOnlySet<string> keys = await acl.GetKeysAsync();
            return Ok(new AclKeysResponse { UserId = userId, Keys = keys.ToList() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting keys for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving security keys.");
        }
    }

    /// <summary>
    /// Grant a security key to a user.
    /// </summary>
    [HttpPost("users/{userId}/keys")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> GrantKey(string userId, [FromBody] AclGrantKeyRequest request)
    {
        try
        {
            string adminId = GetCurrentUserId();
            string adminName = GetCurrentUserName();

            IAccessControlGrain acl = GetAclGrain(userId);
            await acl.GrantKeyAsync(request.KeyName, adminId, adminName, request.Reason);

            _logger.LogInformation(
                "Security key {KeyName} granted to user {UserId} by {AdminId}",
                request.KeyName, userId, adminId);
            return Created("", new { Message = $"Key {request.KeyName} granted to user {userId}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting key {KeyName} to user {UserId}", request.KeyName, userId);
            return StatusCode(500, "An error occurred while granting security key.");
        }
    }

    /// <summary>
    /// Revoke a security key from a user.
    /// </summary>
    [HttpDelete("users/{userId}/keys/{keyName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeKey(string userId, string keyName, [FromQuery] string? reason = null)
    {
        try
        {
            string adminId = GetCurrentUserId();
            string adminName = GetCurrentUserName();

            IAccessControlGrain acl = GetAclGrain(userId);
            await acl.RevokeKeyAsync(keyName, adminId, adminName, reason);

            _logger.LogInformation(
                "Security key {KeyName} revoked from user {UserId} by {AdminId}",
                keyName, userId, adminId);
            return Ok(new { Message = $"Key {keyName} revoked from user {userId}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking key {KeyName} from user {UserId}", keyName, userId);
            return StatusCode(500, "An error occurred while revoking security key.");
        }
    }

    /// <summary>
    /// Bulk-set all security keys for a user (replaces existing keys).
    /// </summary>
    [HttpPut("users/{userId}/keys")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetKeys(string userId, [FromBody] AclSetKeysRequest request)
    {
        try
        {
            string adminId = GetCurrentUserId();
            string adminName = GetCurrentUserName();

            IAccessControlGrain acl = GetAclGrain(userId);
            await acl.SetKeysAsync(request.Keys, adminId, adminName, request.Reason);

            _logger.LogInformation(
                "Security keys bulk-set for user {UserId} by {AdminId}: [{Keys}]",
                userId, adminId, string.Join(", ", request.Keys));
            return Ok(new { Message = $"Keys updated for user {userId}.", KeyCount = request.Keys.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting keys for user {UserId}", userId);
            return StatusCode(500, "An error occurred while setting security keys.");
        }
    }

    /// <summary>
    /// Get the key audit log for a user (grant/revoke history).
    /// </summary>
    [HttpGet("users/{userId}/keys/audit")]
    [ProducesResponseType(typeof(List<SecurityKeyAuditEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SecurityKeyAuditEntry>>> GetKeyAuditLog(string userId)
    {
        try
        {
            IAccessControlGrain acl = GetAclGrain(userId);
            List<SecurityKeyAuditEntry> log = await acl.GetKeyAuditLogAsync();
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key audit log for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving key audit log.");
        }
    }

    // ─── Session Administration ─────────────────────────────────────────

    /// <summary>
    /// Get the full access control state for a user (keys + session info).
    /// </summary>
    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(AccessControlState), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessControlState>> GetAccessControlState(string userId)
    {
        try
        {
            IAccessControlGrain acl = GetAclGrain(userId);
            AccessControlState state = await acl.GetAccessControlStateAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access control state for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving access control state.");
        }
    }

    /// <summary>
    /// Force-end a user's session (admin action — e.g., compromised account).
    /// </summary>
    [HttpPost("users/{userId}/session/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForceEndSession(string userId)
    {
        try
        {
            IAccessControlGrain acl = GetAclGrain(userId);
            await acl.EndSessionAsync();

            _logger.LogWarning("Session force-ended for user {UserId} by admin {AdminId}", userId, GetCurrentUserId());
            return Ok(new { Message = $"Session ended for user {userId}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session for user {UserId}", userId);
            return StatusCode(500, "An error occurred while ending session.");
        }
    }

    // ─── Reference: Available Security Keys ─────────────────────────────

    /// <summary>
    /// Get all available security key definitions.
    /// Returns the well-known VistA security keys and their descriptions.
    /// </summary>
    [HttpGet("keys")]
    [ProducesResponseType(typeof(List<AclKeyDefinition>), StatusCodes.Status200OK)]
    public ActionResult<List<AclKeyDefinition>> GetAvailableKeys()
    {
        var keys = new List<AclKeyDefinition>
        {
            new("ORES", "Order Entry (physician)", "Order Entry"),
            new("ORELSE", "Order Entry nurse/clerk mode", "Order Entry"),
            new("OREMAS", "Order Entry master (admin)", "Order Entry"),
            new("PROVIDER", "General provider actions", "Provider"),
            new("PSO PHARMACY", "Outpatient pharmacy", "Pharmacy"),
            new("PSJ RPHARM", "Inpatient pharmacy", "Pharmacy"),
            new("PSA ORDERS", "Drug accountability", "Pharmacy"),
            new("PSB MANAGER", "BCMA manager", "Pharmacy"),
            new("LRLAB", "General lab access", "Laboratory"),
            new("LRVERIFY", "Lab result verification", "Laboratory"),
            new("LRSU", "Lab supervisor", "Laboratory"),
            new("RA VERIFY", "Radiology verification", "Radiology"),
            new("RA TECHNOLOGIST", "Radiology technologist", "Radiology"),
            new("TIU SIGN", "Sign clinical documents", "Documentation"),
            new("TIU COSIGN", "Cosign clinical documents", "Documentation"),
            new("GMRA ALLERGY", "Allergy/ADR management", "Clinical"),
            new("GMRV VITALS", "Vitals entry", "Clinical"),
            new("GMPL PROBLEM", "Problem list management", "Clinical"),
            new("SD SCHEDULING", "Appointment scheduling", "Scheduling"),
            new("DG ADMIT", "ADT operations", "ADT"),
            new("DG SENSITIVITY", "Patient sensitivity management", "ADT"),
            new("SR SURGERY", "Surgery package access", "Surgery"),
            new("GMRC CONSULTS", "Consult management", "Consults"),
            new("YS MH INSTRUMENT", "Mental health instruments", "Mental Health"),
            new("XUPROG", "Programmer access (superuser)", "System"),
            new("XUMGR", "System manager", "System"),
            new("XUAUDIT", "Audit trail management", "System"),
            new("FHIR ACCESS", "FHIR API access", "Interoperability"),
            new("MPI MANAGER", "MPI management", "Interoperability"),
        };
        return Ok(keys);
    }

    // ─── Demo data ──────────────────────────────────────────────────────

    /// <summary>
    /// Load demo security keys for all seeded demo users.
    /// Maps ASP.NET Core Identity roles to VistA security keys.
    /// </summary>
    [HttpPost("demo/load")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadDemo()
    {
        try
        {
            string adminId = GetCurrentUserId();
            string adminName = GetCurrentUserName();

            // Role → security key mappings (VistA-faithful)
            var roleKeyMap = new Dictionary<string, string[]>
            {
                ["Provider"] = [SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.TIU_SIGN, SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMRV_VITALS, SecurityKeys.GMPL_PROBLEM],
                ["Nurse"] = [SecurityKeys.ORELSE, SecurityKeys.GMRV_VITALS, SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMPL_PROBLEM, SecurityKeys.SD_SCHEDULING],
                ["Pharmacist"] = [SecurityKeys.PSO_PHARMACY, SecurityKeys.PSJ_RPHARM, SecurityKeys.PSA_ORDERS, SecurityKeys.PSB_MANAGER],
                ["LabTechnician"] = [SecurityKeys.LRLAB, SecurityKeys.LRVERIFY],
                ["Radiologist"] = [SecurityKeys.RA_VERIFY, SecurityKeys.PROVIDER, SecurityKeys.TIU_SIGN],
                ["Surgeon"] = [SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.TIU_SIGN, SecurityKeys.SR_SURGERY],
                ["Administrator"] = [SecurityKeys.XUMGR, SecurityKeys.XUAUDIT, SecurityKeys.DG_SENSITIVITY],
                ["RegistrationClerk"] = [SecurityKeys.SD_SCHEDULING, SecurityKeys.DG_ADMIT],
                ["MentalHealth"] = [SecurityKeys.PROVIDER, SecurityKeys.TIU_SIGN, SecurityKeys.YS_MH_INSTRUMENT],
            };

            // Look up demo users from the NewPerson grain index
            // We use well-known user IDs from the demo seed
            var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Infrastructure.NewVistasUser>>();
            var users = userManager.Users.ToList();
            int keysGranted = 0;

            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                var allKeys = new HashSet<string>();
                foreach (string role in roles)
                {
                    if (roleKeyMap.TryGetValue(role, out string[]? keys))
                        allKeys.UnionWith(keys);
                }

                if (allKeys.Count > 0)
                {
                    IAccessControlGrain acl = GetAclGrain(user.Id);
                    await acl.SetKeysAsync(allKeys.ToList(), adminId, adminName, "Demo provisioning");
                    await acl.StartSessionAsync(null, null, "DEMO", "127.0.0.1");
                    keysGranted += allKeys.Count;
                }
            }

            return Ok(new { Message = $"Demo keys loaded for {users.Count} users ({keysGranted} total key assignments)." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo security keys");
            return StatusCode(500, "An error occurred while loading demo data.");
        }
    }
}

// ─── Request/Response DTOs ───────────────────────────────────────────────

public record AclKeysResponse
{
    public required string UserId { get; init; }
    public required List<string> Keys { get; init; }
}

public record AclGrantKeyRequest
{
    public required string KeyName { get; init; }
    public string? Reason { get; init; }
}

public record AclSetKeysRequest
{
    public required List<string> Keys { get; init; }
    public string? Reason { get; init; }
}

public record AclKeyDefinition(string KeyName, string Description, string Category);
