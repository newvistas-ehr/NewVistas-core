// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NewVistas.Abstractions.GrainInterfaces;

namespace NewVistas.PatientPortal.Controllers;

/// <summary>
/// Patient portal authentication — register, login, profile.
/// Separate from clinician auth. Patients authenticate with patientId + password
/// and receive a JWT containing a "patient_id" claim scoped to their record.
/// </summary>
[ApiController]
[Route("api/patient-auth")]
public class PatientAuthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PatientAuthController> _logger;

    public PatientAuthController(
        IGrainFactory grainFactory,
        IConfiguration configuration,
        ILogger<PatientAuthController> logger)
    {
        _grainFactory = grainFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Register a new patient portal account.
    /// The patient must provide their patient ID (verified to exist) plus email and password.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult> Register([FromBody] PatientRegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PatientId) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { Error = "PatientId, Email, and Password are required." });

            if (request.Password.Length < 8)
                return BadRequest(new { Error = "Password must be at least 8 characters." });

            // Verify the patient exists in the system
            var patientGrain = _grainFactory.GetGrain<IPatientGrain>(request.PatientId);
            var patient = await patientGrain.GetPatientAsync();
            if (string.IsNullOrEmpty(patient.Name))
                return BadRequest(new { Error = "Patient ID not found. Contact your care team for assistance." });

            // Create portal account
            var accountGrain = _grainFactory.GetGrain<IPatientAccountGrain>($"PORTAL-ACCT:{request.PatientId}");
            bool alreadyRegistered = await accountGrain.IsRegisteredAsync();
            if (alreadyRegistered)
                return Conflict(new { Error = "An account already exists for this patient ID." });

            string passwordHash = HashPassword(request.Password);
            bool registered = await accountGrain.RegisterAsync(
                request.Email, passwordHash, request.DisplayName ?? patient.Name);

            if (!registered)
                return Conflict(new { Error = "Registration failed." });

            _logger.LogInformation("Patient portal account registered for {PatientId}", request.PatientId);
            return Created("", new { PatientId = request.PatientId, Email = request.Email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering patient portal account for {PatientId}", request.PatientId);
            return StatusCode(500, "An error occurred during registration.");
        }
    }

    /// <summary>
    /// Authenticate patient and receive a JWT with patient_id claim.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult> Login([FromBody] PatientLoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PatientId) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { Error = "PatientId and Password are required." });

            var accountGrain = _grainFactory.GetGrain<IPatientAccountGrain>($"PORTAL-ACCT:{request.PatientId}");
            bool isRegistered = await accountGrain.IsRegisteredAsync();
            if (!isRegistered)
                return Unauthorized(new { Error = "Invalid credentials." });

            string passwordHash = HashPassword(request.Password);
            bool valid = await accountGrain.VerifyCredentialsAsync(passwordHash);
            if (!valid)
                return Unauthorized(new { Error = "Invalid credentials." });

            var account = await accountGrain.GetAccountAsync();
            if (!account.IsActive)
                return Unauthorized(new { Error = "Account is deactivated. Contact your care team." });

            await accountGrain.RecordLoginAsync();

            string token = GeneratePatientJwt(request.PatientId, account.DisplayName, account.Email);

            _logger.LogInformation("Patient {PatientId} logged in to portal", request.PatientId);
            return Ok(new
            {
                Token = token,
                PatientId = request.PatientId,
                DisplayName = account.DisplayName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during patient portal login for {PatientId}", request.PatientId);
            return StatusCode(500, "An error occurred during login.");
        }
    }

    /// <summary>
    /// Get the current patient's profile.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult> GetCurrentPatient()
    {
        try
        {
            string patientId = GetPatientId();
            var accountGrain = _grainFactory.GetGrain<IPatientAccountGrain>($"PORTAL-ACCT:{patientId}");
            var account = await accountGrain.GetAccountAsync();

            var patientGrain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await patientGrain.GetPatientAsync();

            return Ok(new
            {
                PatientId = patientId,
                account.DisplayName,
                account.Email,
                PatientName = patient.Name,
                DateOfBirth = patient.DateOfBirth,
                account.LastLoginDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patient profile");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private string GetPatientId()
        => User.FindFirstValue("patient_id")
            ?? throw new InvalidOperationException("patient_id claim not found in JWT.");

    private string GeneratePatientJwt(string patientId, string displayName, string email)
    {
        string jwtKey = _configuration["Jwt:Key"] ?? "NewVistas-Patient-Portal-Key-Must-Be-32-Bytes!";
        string jwtIssuer = _configuration["Jwt:Issuer"] ?? "NewVistas-PatientPortal";
        string jwtAudience = _configuration["Jwt:Audience"] ?? "NewVistas-PatientPortal";
        int expirationMinutes = _configuration.GetValue<int?>("Jwt:ExpirationMinutes") ?? 480;

        var claims = new List<Claim>
        {
            new("patient_id", patientId),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, "Patient"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hash);
    }
}

// ─── Request DTOs ───────────────────────────────────────────────────────────

public record PatientRegisterRequest
{
    public required string PatientId { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? DisplayName { get; init; }
}

public record PatientLoginRequest
{
    public required string PatientId { get; init; }
    public required string Password { get; init; }
}
