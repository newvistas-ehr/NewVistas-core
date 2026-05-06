// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Authentication controller — Layer 1 (replaces VistA Access/Verify codes).
/// Handles user registration, login, MFA (§170.315(d)(13)), token refresh,
/// and electronic signature management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<NewVistasUser> _userManager;
    private readonly SignInManager<NewVistasUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IGrainFactory _grainFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<NewVistasUser> userManager,
        SignInManager<NewVistasUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IGrainFactory grainFactory,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _grainFactory = grainFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account and create the associated NewPersonGrain.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = new NewVistasUser
            {
                UserName = request.UserName,
                Email = request.Email,
                DisplayName = request.DisplayName
            };

            IdentityResult result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

            // Assign roles
            if (request.Roles != null)
            {
                foreach (string role in request.Roles)
                {
                    if (!await _roleManager.RoleExistsAsync(role))
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    await _userManager.AddToRoleAsync(user, role);
                }
            }

            // Create the NewPersonGrain with staff directory info
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{user.Id}");
            await personGrain.UpdateProfileAsync(
                name: request.DisplayName,
                title: request.Title,
                degree: request.Degree,
                serviceSection: request.ServiceSection,
                userClass: request.UserClass,
                providerType: request.ProviderType,
                specialty: request.Specialty,
                institutionId: null,
                institutionName: null,
                divisionId: null,
                divisionName: null);

            _logger.LogInformation("User {UserName} registered with ID {UserId}", user.UserName, user.Id);
            return Created("", new { UserId = user.Id, UserName = user.UserName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user {UserName}", request.UserName);
            return StatusCode(500, "An error occurred during registration.");
        }
    }

    /// <summary>
    /// Authenticate and receive a JWT token.
    /// Replaces VistA Access Code + Verify Code login (XUS SIGNON).
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            NewVistasUser? user = await _userManager.FindByNameAsync(request.UserName);
            if (user == null)
                return Unauthorized(new { Error = "Invalid credentials." });

            Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    return Unauthorized(new { Error = "Account is locked. Try again later." });
                return Unauthorized(new { Error = "Invalid credentials." });
            }

            // Check NewPersonGrain active status
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{user.Id}");
            bool isActive = await personGrain.IsActiveAsync();
            if (!isActive)
                return Unauthorized(new { Error = "Account is deactivated." });

            var personSummary = await personGrain.GetSummaryAsync();

            // §170.315(d)(13) — MFA challenge: if MFA is enabled, return a challenge
            // instead of a full token. The client must call /api/auth/mfa/verify to complete login.
            if (personSummary.IsMfaEnabled)
            {
                // Issue a short-lived MFA challenge token (5 minutes) that can only be used
                // to complete the MFA step — it carries a special "mfa_pending" claim.
                string mfaChallengeToken = await GenerateMfaChallengeTokenAsync(user);
                _logger.LogInformation("MFA challenge issued for {UserName}", user.UserName);
                return Ok(new MfaChallengeResponse
                {
                    MfaRequired = true,
                    MfaChallengeToken = mfaChallengeToken,
                    UserId = user.Id
                });
            }

            string token = await GenerateJwtTokenAsync(user);

            // Start an Orleans session — sets HasActiveSession=true on the user's ACL grain.
            // The AuthorizationCallFilter checks this to enforce session timeout (§170.315(d)(5)).
            // Mirrors VistA SETUP^XUSRB (post-login session initialization, Sign-on Log File #3.081).
            string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            string? clientDevice = HttpContext.Request.Headers.UserAgent.FirstOrDefault();
            var aclGrain = _grainFactory.GetGrain<IAccessControlGrain>($"ACL:{user.Id}");
            await aclGrain.StartSessionAsync(
                divisionId: null,
                divisionName: null,
                clientDevice: clientDevice,
                clientIpAddress: clientIp);

            _logger.LogInformation("User {UserName} logged in", user.UserName);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                UserName = user.UserName!,
                DisplayName = user.DisplayName,
                UserClass = personSummary.UserClass,
                HasElectronicSignature = personSummary.HasElectronicSignature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {UserName}", request.UserName);
            return StatusCode(500, "An error occurred during login.");
        }
    }

    /// <summary>
    /// End the current user's Orleans session (explicit logout).
    /// Mirrors VistA HALT^XUSCLEAN (sign-off routine).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout()
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var aclGrain = _grainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");
            await aclGrain.EndSessionAsync();

            _logger.LogInformation("User {UserId} logged out", userId);
            return Ok(new { Message = "Logged out successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "An error occurred during logout.");
        }
    }

    /// <summary>
    /// Get the current user's profile from Identity + NewPersonGrain.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult> GetCurrentUser()
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            NewVistasUser? user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");
            var person = await personGrain.GetPersonAsync();
            IList<string> roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                UserId = userId,
                UserName = user.UserName,
                user.DisplayName,
                user.Email,
                Roles = roles,
                Person = person
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user profile");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Electronic Signature (Layer 4) ──────────────────────────────────

    /// <summary>
    /// Set the current user's electronic signature code.
    /// The raw code is hashed before storage — never stored in plain text.
    /// Mirrors VistA XUESSO.m ESSAVE entry point.
    /// </summary>
    [HttpPost("signature/set")]
    [Authorize]
    public async Task<ActionResult> SetElectronicSignature([FromBody] SetSignatureRequest request)
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string hash = HashSignatureCode(request.SignatureCode);

            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");
            await personGrain.SetElectronicSignatureHashAsync(hash);

            _logger.LogInformation("User {UserId} set electronic signature", userId);
            return Ok(new { Message = "Electronic signature set successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting electronic signature");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>
    /// Verify the current user's electronic signature code.
    /// Called at the moment of signing clinical actions (orders, notes, etc.).
    /// Mirrors VistA XUSESIG ESVERIFY entry point.
    /// </summary>
    [HttpPost("signature/verify")]
    [Authorize]
    public async Task<ActionResult> VerifyElectronicSignature([FromBody] VerifySignatureRequest request)
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string hash = HashSignatureCode(request.SignatureCode);

            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");
            bool valid = await personGrain.VerifyElectronicSignatureAsync(hash);

            return Ok(new { Valid = valid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying electronic signature");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Multi-Factor Authentication (§170.315(d)(13)) ─────────────────

    /// <summary>
    /// Complete MFA login — verify TOTP code or backup code after password authentication.
    /// Called with the MFA challenge token from the login response.
    /// </summary>
    [HttpPost("mfa/verify")]
    public async Task<ActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        try
        {
            // Validate the MFA challenge token
            string? userId = ValidateMfaChallengeToken(request.MfaChallengeToken);
            if (userId == null)
                return Unauthorized(new { Error = "Invalid or expired MFA challenge." });

            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");

            // Try TOTP code first, then backup code
            bool valid = await personGrain.VerifyTotpCodeAsync(request.Code);
            if (!valid)
                valid = await personGrain.UseBackupCodeAsync(request.Code);

            if (!valid)
                return Unauthorized(new { Error = "Invalid MFA code." });

            // MFA verified — issue full session token
            NewVistasUser? user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { Error = "User not found." });

            string token = await GenerateJwtTokenAsync(user);
            var personSummary = await personGrain.GetSummaryAsync();

            string? clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            string? clientDevice = HttpContext.Request.Headers.UserAgent.FirstOrDefault();
            var aclGrain = _grainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");
            await aclGrain.StartSessionAsync(
                divisionId: null,
                divisionName: null,
                clientDevice: clientDevice,
                clientIpAddress: clientIp);

            _logger.LogInformation("User {UserId} completed MFA login", userId);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = userId,
                UserName = user.UserName!,
                DisplayName = user.DisplayName,
                UserClass = personSummary.UserClass,
                HasElectronicSignature = personSummary.HasElectronicSignature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MFA verification");
            return StatusCode(500, "An error occurred during MFA verification.");
        }
    }

    /// <summary>
    /// Begin MFA setup — generate a TOTP secret for the current user.
    /// Returns the secret and a provisioning URI for authenticator apps.
    /// MFA is not enforced until /mfa/enable is called with a valid code.
    /// </summary>
    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<ActionResult> SetupMfa()
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string userName = User.FindFirstValue(ClaimTypes.Name)!;
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");

            string secret = await personGrain.SetupMfaAsync();

            // otpauth URI for authenticator apps (Google Authenticator, Authy, etc.)
            string provisioningUri = $"otpauth://totp/NewVistas:{Uri.EscapeDataString(userName)}?secret={secret}&issuer=NewVistas&digits=6&period=30";

            _logger.LogInformation("MFA setup initiated for {UserId}", userId);
            return Ok(new { Secret = secret, ProvisioningUri = provisioningUri });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MFA setup");
            return StatusCode(500, "An error occurred during MFA setup.");
        }
    }

    /// <summary>
    /// Enable MFA enforcement — requires a valid TOTP code to confirm setup.
    /// Also generates and returns 10 one-time backup codes.
    /// </summary>
    [HttpPost("mfa/enable")]
    [Authorize]
    public async Task<ActionResult> EnableMfa([FromBody] MfaEnableRequest request)
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");

            bool enabled = await personGrain.EnableMfaAsync(request.TotpCode);
            if (!enabled)
                return BadRequest(new { Error = "Invalid TOTP code. MFA not enabled." });

            // Generate backup codes
            List<string> backupCodes = await personGrain.GenerateBackupCodesAsync();

            _logger.LogInformation("MFA enabled for {UserId}", userId);
            return Ok(new
            {
                Message = "MFA enabled successfully.",
                BackupCodes = backupCodes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling MFA");
            return StatusCode(500, "An error occurred enabling MFA.");
        }
    }

    /// <summary>
    /// Disable MFA for the current user.
    /// </summary>
    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<ActionResult> DisableMfa()
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");

            await personGrain.DisableMfaAsync();

            _logger.LogInformation("MFA disabled for {UserId}", userId);
            return Ok(new { Message = "MFA disabled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling MFA");
            return StatusCode(500, "An error occurred disabling MFA.");
        }
    }

    /// <summary>
    /// Get the current user's MFA status.
    /// </summary>
    [HttpGet("mfa/status")]
    [Authorize]
    public async Task<ActionResult> GetMfaStatus()
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");

            bool enabled = await personGrain.IsMfaEnabledAsync();
            return Ok(new { IsMfaEnabled = enabled });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MFA status");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>
    /// Regenerate backup codes (invalidates existing ones).
    /// </summary>
    [HttpPost("mfa/backup-codes")]
    [Authorize]
    public async Task<ActionResult> RegenerateBackupCodes()
    {
        try
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var personGrain = _grainFactory.GetGrain<Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{userId}");

            bool enabled = await personGrain.IsMfaEnabledAsync();
            if (!enabled)
                return BadRequest(new { Error = "MFA is not enabled." });

            List<string> codes = await personGrain.GenerateBackupCodesAsync();

            _logger.LogInformation("Backup codes regenerated for {UserId}", userId);
            return Ok(new { BackupCodes = codes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating backup codes");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Admin: Seed demo roles ──────────────────────────────────────────

    /// <summary>
    /// Seed the standard VistA-mapped roles for development.
    /// </summary>
    [HttpPost("seed-roles")]
    public async Task<ActionResult> SeedRoles()
    {
        string[] roles = new[]
        {
            "Provider",           // PROVIDER key — physicians, NPs, PAs
            "Nurse",              // Nursing staff
            "Pharmacist",         // PSJ RPHARM key — pharmacy verification
            "RegistrationClerk",  // DG REGISTER key — patient registration
            "OrderEntry",         // ORES key — order entry
            "LabTechnician",      // Lab processing staff
            "Radiologist",        // Radiology reads
            "Surgeon",            // Surgical team
            "MentalHealth",       // Mental health providers
            "SocialWorker",       // Social work services
            "Dietitian",          // Nutrition services
            "Administrator",      // System admin (no clinical actions)
            "ChiefOfStaff",       // Administrative oversight
            "PrivacyOfficer",     // HIPAA privacy compliance
            "ARSupervisor"        // PRCA AR SUPERVISOR — accounts receivable
        };

        foreach (string role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }

        return Ok(new { Message = $"Seeded {roles.Length} roles.", Roles = roles });
    }

    // ─── Private helpers ─────────────────────────────────────────────────

    private async Task<string> GenerateJwtTokenAsync(NewVistasUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new("display_name", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (string role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        string jwtKey = _configuration["Jwt:Key"] ?? "NewVistas-Dev-Key-Must-Be-At-Least-32-Bytes!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        int expirationMinutes = _configuration.GetValue<int?>("Jwt:ExpirationMinutes") ?? 480; // 8 hours default

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "NewVistas",
            audience: _configuration["Jwt:Audience"] ?? "NewVistas",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Hash the electronic signature code using SHA-256.
    /// Mirrors VistA's hashing of the ELECTRONIC SIGNATURE CODE field.
    /// </summary>
    private static string HashSignatureCode(string signatureCode)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(signatureCode));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Generate a short-lived JWT for the MFA challenge step (5 minutes).
    /// Contains only the user ID and a special "mfa_pending" claim — NOT a full session token.
    /// </summary>
    private Task<string> GenerateMfaChallengeTokenAsync(NewVistasUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new("mfa_pending", "true"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        string jwtKey = _configuration["Jwt:Key"] ?? "NewVistas-Dev-Key-Must-Be-At-Least-32-Bytes!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "NewVistas",
            audience: _configuration["Jwt:Audience"] ?? "NewVistas",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    /// <summary>
    /// Validate an MFA challenge token and extract the user ID.
    /// Returns null if the token is invalid, expired, or not an MFA challenge token.
    /// </summary>
    private string? ValidateMfaChallengeToken(string token)
    {
        try
        {
            string jwtKey = _configuration["Jwt:Key"] ?? "NewVistas-Dev-Key-Must-Be-At-Least-32-Bytes!";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var tokenHandler = new JwtSecurityTokenHandler();
            ClaimsPrincipal principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "NewVistas",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "NewVistas",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            // Must be an MFA challenge token
            string? mfaPending = principal.FindFirstValue("mfa_pending");
            if (mfaPending != "true")
                return null;

            return principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch
        {
            return null;
        }
    }
}

// ─── Request/Response DTOs ───────────────────────────────────────────────

public record RegisterRequest
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string[]? Roles { get; init; }
    public string? Title { get; init; }
    public string? Degree { get; init; }
    public string? ServiceSection { get; init; }
    public string? UserClass { get; init; }
    public string? ProviderType { get; init; }
    public string? Specialty { get; init; }
}

public record LoginRequest
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
}

public record LoginResponse
{
    public required string Token { get; init; }
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public string? UserClass { get; init; }
    public bool HasElectronicSignature { get; init; }
}

public record SetSignatureRequest
{
    public required string SignatureCode { get; init; }
}

public record VerifySignatureRequest
{
    public required string SignatureCode { get; init; }
}

// ─── MFA DTOs (§170.315(d)(13)) ─────────────────────────────────────────

public record MfaChallengeResponse
{
    public bool MfaRequired { get; init; } = true;
    public required string MfaChallengeToken { get; init; }
    public required string UserId { get; init; }
}

public record MfaVerifyRequest
{
    public required string MfaChallengeToken { get; init; }
    public required string Code { get; init; }
}

public record MfaEnableRequest
{
    public required string TotpCode { get; init; }
}
